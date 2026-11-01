// SPDX-License-Identifier: GPL-3.0-or-later
// Copied into the pinned ShareX fork by scripts/prepare-fork.ps1.
using ShareX.Receipts;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShareX
{
    internal sealed class ResearchCaptureReceipt
    {
        private readonly byte[] original;
        private readonly PixelIdentity originalPixels;
        private readonly List<RecipeStage> stages = new List<RecipeStage>();
        private readonly TaskSettings settings;

        public ResearchCaptureReceipt(Bitmap input, TaskSettings taskSettings)
        {
            settings = taskSettings;
            original = BitmapReceipt.Png(input);
            originalPixels = BitmapReceipt.Pixels(input);
            Observe("worker-input", input, "Accepted bitmap before WorkerTask after-capture processing.");
        }

        public void Observe(string name, Bitmap image, string note)
        {
            stages.Add(new RecipeStage(name, "observed", BitmapReceipt.Pixels(image), note));
        }

        public ReceiptWriteResult Seal(string finalFile)
        {
            PixelIdentity outputPixels = null;
            try { using (var image = new Bitmap(finalFile)) outputPixels = BitmapReceipt.Pixels(image); }
            catch (ArgumentException) { /* External actions may produce a non-image. Its bytes remain committed. */ }
            var imageSettings = settings.ImageSettingsReference;
            var recipe = new CaptureRecipe(
                "WorkerTask input bitmap before after-capture effects; sealed after file actions and before OCR/upload/delete",
                Enum.GetValues(typeof(AfterCaptureTasks)).Cast<AfterCaptureTasks>()
                    .Where(v => v != AfterCaptureTasks.None && settings.AfterCaptureJob.HasFlag(v)).Select(v => v.ToString()).ToArray(),
                new Dictionary<string, string>
                {
                    ["configuredImageFormat"] = imageSettings.ImageFormat.ToString(),
                    ["configuredJpegQuality"] = imageSettings.ImageJPEGQuality.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["configuredAutoJpegQuality"] = imageSettings.ImageAutoJPEGQuality.ToString(),
                    ["finalExtension"] = Path.GetExtension(finalFile)
                }, stages.ToArray(), "Source context intentionally omitted; receipt contains captured pixels",
                "Requested tasks are not execution proof. Only listed stages were observed. Region annotations performed before WorkerTask are already in the input. External-action internals, OCR, upload, capture authenticity and complete effect replay are not attested.");
            string sidecar = finalFile + ".receipt-link.json";
            if (File.Exists(sidecar)) throw new IOException("Existing receipt link preserved; choose a new capture filename.");
            var result = ReceiptStore.Create(Path.Combine(Path.GetDirectoryName(finalFile), "Receipts"),
                original, originalPixels, File.ReadAllBytes(finalFile), Path.GetExtension(finalFile),
                outputPixels == null ? "application/octet-stream" : "image/" + Path.GetExtension(finalFile).TrimStart('.'), outputPixels, recipe);
            var link = new ReceiptLink(Path.GetFileName(result.Directory), result.ReceiptSha256);
            // Refuse overwrite: an old anchor must never silently acquire a new meaning.
            using (var stream = new FileStream(sidecar, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, link, ReceiptStore.Json);
            return result;
        }
    }

    internal sealed record ReceiptLink(string ReceiptId, string ReceiptSha256);
}
