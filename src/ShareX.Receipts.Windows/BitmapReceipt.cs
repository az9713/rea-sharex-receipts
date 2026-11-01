// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ShareX.Receipts;

public static class BitmapReceipt
{
    /// <summary>Rows of RGBA8, top to bottom, preceded by domain and little-endian dimensions.</summary>
    public static PixelIdentity Pixels(Bitmap image)
    {
        using Bitmap normalized = image.Clone(new Rectangle(0, 0, image.Width, image.Height), PixelFormat.Format32bppArgb);
        BitmapData data = normalized.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.ASCII.GetBytes("sharex-pixels-v1\n"));
            Span<byte> dimensions = stackalloc byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(dimensions, image.Width);
            BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..], image.Height);
            hash.AppendData(dimensions);
            byte[] row = new byte[checked(image.Width * 4)];
            for (int y = 0; y < image.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                for (int x = 0; x < row.Length; x += 4) (row[x], row[x + 2]) = (row[x + 2], row[x]);
                hash.AppendData(row);
            }
            return new PixelIdentity(image.Width, image.Height, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        finally { normalized.UnlockBits(data); }
    }

    public static byte[] Png(Bitmap image)
    { using var stream = new MemoryStream(); image.Save(stream, ImageFormat.Png); return stream.ToArray(); }

    public static VerificationResult Verify(string directory, string? trustedDigest = null)
    {
        VerificationResult basic = ReceiptStore.Verify(directory, trustedDigest);
        if (!basic.Valid) return basic;
        var issues = new List<VerificationIssue>();
        foreach (var artifact in ReceiptStore.ReadManifest(directory).Artifacts.Where(a => a.Pixels is not null))
        {
            try
            {
                using var image = new Bitmap(Path.Combine(directory, artifact.Name));
                if (Pixels(image) != artifact.Pixels)
                    issues.Add(new("pixel-mismatch", artifact.Name, "Decoded RGBA pixels or dimensions differ from the commitment."));
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or ExternalException)
            { issues.Add(new("pixel-decode-failed", artifact.Name, ex.Message)); }
        }
        return new VerificationResult(issues.Count == 0, basic.ReceiptSha256, issues.ToArray());
    }
}
