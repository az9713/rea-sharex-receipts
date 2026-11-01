// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareX.Receipts;

public sealed record PixelIdentity(int Width, int Height, string Sha256);
public sealed record ReceiptArtifact(string Name, long Bytes, string Sha256, string MediaType, PixelIdentity? Pixels);
public sealed record RecipeStage(string Name, string State, PixelIdentity? Pixels, string Note);
public sealed record CaptureRecipe(string Boundary, string[] RequestedTasks, Dictionary<string, string> Settings,
    RecipeStage[] ObservedStages, string SourceLabel, string Limitations);
public sealed record ReceiptManifest(string Schema, string Id, DateTimeOffset CreatedUtc, string Producer,
    string UpstreamRevision, ReceiptArtifact[] Artifacts);
public sealed record VerificationIssue(string Code, string File, string Detail);
public sealed record VerificationResult(bool Valid, string? ReceiptSha256, VerificationIssue[] Issues);
public sealed record ReceiptWriteResult(string Directory, string ReceiptSha256);

/// <summary>Immutable byte commitments. A receipt hash is an integrity anchor, not a digital signature.</summary>
public static class ReceiptStore
{
    public const string Schema = "sharex-receipt/1";
    public const string UpstreamRevision = "d2502561f63fc3ff502cacd91514e3f7f2948c74";
    public const long MaximumArtifactBytes = 512L * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32
    };

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static ReceiptWriteResult Create(string parent, byte[] originalPng, PixelIdentity originalPixels,
        byte[] result, string resultExtension, string mediaType, PixelIdentity? resultPixels, CaptureRecipe recipe,
        DateTimeOffset? createdUtc = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(originalPixels);
        ValidatePixels(originalPixels);
        if (resultPixels is not null) ValidatePixels(resultPixels);
        CheckLength(originalPng.LongLength); CheckLength(result.LongLength);
        string extension = resultExtension.TrimStart('.').ToLowerInvariant();
        if (extension.Length is < 1 or > 8 || extension.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Result extension must contain 1–8 ASCII letters/digits.");
        string receiptId = id ?? Guid.NewGuid().ToString("N");
        if (receiptId.Length is < 1 or > 64 || receiptId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Receipt ID must contain ASCII letters, digits or hyphens.");
        string root = Path.GetFullPath(parent);
        Directory.CreateDirectory(root);
        RejectReparseChain(root);
        string destination = Path.Combine(root, receiptId);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Receipt already exists.");
        string staging = Path.Combine(root, ".pending-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            byte[] recipeBytes = JsonSerializer.SerializeToUtf8Bytes(recipe, Json);
            var artifacts = new[]
            {
                Artifact("original.png", originalPng, "image/png", originalPixels),
                Artifact("result." + extension, result, mediaType, resultPixels),
                Artifact("recipe.json", recipeBytes, "application/json", null)
            };
            WriteNew(Path.Combine(staging, artifacts[0].Name), originalPng);
            WriteNew(Path.Combine(staging, artifacts[1].Name), result);
            WriteNew(Path.Combine(staging, artifacts[2].Name), recipeBytes);
            var manifest = new ReceiptManifest(Schema, receiptId, createdUtc ?? DateTimeOffset.UtcNow,
                "ShareX Receipts / experimental fork", UpstreamRevision, artifacts);
            byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, Json);
            string digest = Hash(manifestBytes);
            WriteNew(Path.Combine(staging, "receipt.json"), manifestBytes);
            WriteNew(Path.Combine(staging, "receipt.sha256"), Encoding.ASCII.GetBytes(digest + "  receipt.json\n"));
            Directory.Move(staging, destination);
            return new ReceiptWriteResult(destination, digest);
        }
        catch
        {
            // Only this newly created staging directory is owned by this call.
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            throw;
        }
    }

    public static VerificationResult Verify(string directory, string? expectedReceiptSha256 = null)
    {
        var issues = new List<VerificationIssue>();
        string? digest = null;
        try
        {
            string root = Path.GetFullPath(directory);
            RejectReparseChain(root);
            string manifestPath = Path.Combine(root, "receipt.json");
            RequireOrdinaryFile(manifestPath, 1024 * 1024);
            byte[] bytes = File.ReadAllBytes(manifestPath);
            digest = Hash(bytes);
            string anchorPath = Path.Combine(root, "receipt.sha256");
            RequireOrdinaryFile(anchorPath, 256);
            string declared = File.ReadAllText(anchorPath, Encoding.ASCII).Trim();
            if (declared != digest + "  receipt.json")
                issues.Add(new("receipt-hash-mismatch", "receipt.json", "Receipt differs from its companion hash."));
            if (expectedReceiptSha256 is not null && !string.Equals(expectedReceiptSha256, digest, StringComparison.OrdinalIgnoreCase))
                issues.Add(new("trusted-anchor-mismatch", "receipt.json", "Receipt differs from the independently retained digest."));
            ReceiptManifest manifest = JsonSerializer.Deserialize<ReceiptManifest>(bytes, Json)
                ?? throw new InvalidDataException("Receipt is null.");
            ValidateManifest(manifest);
            var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "receipt.json", "receipt.sha256" };
            foreach (ReceiptArtifact artifact in manifest.Artifacts)
            {
                if (!expectedNames.Add(artifact.Name)) throw new InvalidDataException("Duplicate artifact name.");
                string path = Path.Combine(root, artifact.Name);
                try
                {
                    RequireOrdinaryFile(path, MaximumArtifactBytes);
                    using var stream = File.OpenRead(path);
                    if (stream.Length != artifact.Bytes) issues.Add(new("size-mismatch", artifact.Name, "Artifact length differs."));
                    string actual = Convert.ToHexStringLower(SHA256.HashData(stream));
                    if (actual != artifact.Sha256) issues.Add(new("artifact-hash-mismatch", artifact.Name, "Artifact bytes differ."));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { issues.Add(new("artifact-unavailable", artifact.Name, ex.Message)); }
            }
            foreach (string path in Directory.EnumerateFileSystemEntries(root))
                if (!expectedNames.Contains(Path.GetFileName(path)))
                    issues.Add(new("uncommitted-entry", Path.GetFileName(path), "Entry is not committed by this receipt."));
            if (issues.Count == 0)
            {
                string recipePath = Path.Combine(root, "recipe.json");
                RequireOrdinaryFile(recipePath, 1024 * 1024);
                var recipe = JsonSerializer.Deserialize<CaptureRecipe>(File.ReadAllBytes(recipePath), Json)
                    ?? throw new InvalidDataException("Recipe is null.");
                if (recipe.ObservedStages is null || recipe.RequestedTasks is null || recipe.Settings is null || string.IsNullOrWhiteSpace(recipe.Boundary))
                    throw new InvalidDataException("Recipe lacks its boundary or stage/task/settings declarations.");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NullReferenceException)
        { issues.Add(new("invalid-receipt", "receipt.json", ex.Message)); }
        return new VerificationResult(issues.Count == 0, digest, issues.ToArray());
    }

    public static ReceiptManifest ReadManifest(string directory)
    {
        string path = Path.Combine(Path.GetFullPath(directory), "receipt.json");
        RequireOrdinaryFile(path, 1024 * 1024);
        var manifest = JsonSerializer.Deserialize<ReceiptManifest>(File.ReadAllBytes(path), Json)
            ?? throw new InvalidDataException("Receipt is null.");
        ValidateManifest(manifest);
        return manifest;
    }

    private static ReceiptArtifact Artifact(string name, byte[] bytes, string type, PixelIdentity? pixels) =>
        new(name, bytes.LongLength, Hash(bytes), type, pixels);
    private static void CheckLength(long length)
    { if (length <= 0 || length > MaximumArtifactBytes) throw new InvalidDataException("Artifact length outside 1–512 MiB."); }
    private static bool IsDigest(string? hash) => hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void ValidatePixels(PixelIdentity p)
    { if (p.Width is <= 0 or > 100000 || p.Height is <= 0 or > 100000 || !IsDigest(p.Sha256)) throw new InvalidDataException("Invalid pixel identity."); }
    private static void ValidateManifest(ReceiptManifest m)
    {
        if (m.Schema != Schema || string.IsNullOrWhiteSpace(m.Id) || m.Artifacts is not { Length: 3 })
            throw new InvalidDataException("Unsupported schema or malformed receipt.");
        if (m.Artifacts[0]?.Name != "original.png" || m.Artifacts[1] is null || !m.Artifacts[1].Name.StartsWith("result.", StringComparison.Ordinal)
            || m.Artifacts[2]?.Name != "recipe.json") throw new InvalidDataException("Expected original, result and recipe artifacts in order.");
        foreach (var a in m.Artifacts)
        {
            if (a is null || string.IsNullOrEmpty(a.Name) || a.Name.Length > 80 || a.Name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-')
                || a.Name.Contains("..", StringComparison.Ordinal) || a.Name != Path.GetFileName(a.Name) || !IsDigest(a.Sha256))
                throw new InvalidDataException("Invalid artifact name or digest.");
            CheckLength(a.Bytes);
            if (a.Pixels is not null) ValidatePixels(a.Pixels);
        }
        if (m.Artifacts[0].Pixels is null) throw new InvalidDataException("Original pixel commitment is required.");
    }
    private static void RequireOrdinaryFile(string path, long maximum)
    {
        var info = new FileInfo(path);
        if (!info.Exists || (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new IOException("Expected ordinary non-linked file: " + Path.GetFileName(path));
        if (info.Length <= 0 || info.Length > maximum) throw new InvalidDataException("File exceeds its declared read budget.");
    }
    private static void RejectReparseChain(string root)
    {
        var path = new DirectoryInfo(root);
        if (!path.Exists) throw new DirectoryNotFoundException("Receipt directory does not exist.");
        if ((path.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked receipt directory is not admitted.");
    }
    private static void WriteNew(string path, byte[] bytes)
    { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes); stream.Flush(true); }
}
