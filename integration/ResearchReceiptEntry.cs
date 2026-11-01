// SPDX-License-Identifier: GPL-3.0-or-later
using ShareX.Receipts;
using ShareX.HelpersLib;
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ShareX
{
    // These isolated modes run before upstream profile/startup/registry handling.
    internal static class ResearchReceiptEntry
    {
        public static bool TryRun(string[] args)
        {
            if (args.Length != 2 || (args[0] != "--receipt-self-test" && args[0] != "--receipt-inspector")) return false;
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            if (args[0] == "--receipt-inspector")
            {
                Application.Run(new ResearchReceiptForm(Path.GetFullPath(args[1])));
                return true;
            }
            try
            {
                string root = Path.GetFullPath(args[1]);
                Directory.CreateDirectory(root);
                using var original = new Bitmap(640, 360);
                using (var graphics = Graphics.FromImage(original))
                {
                    graphics.Clear(Color.FromArgb(13, 24, 38));
                    graphics.FillRectangle(Brushes.DeepSkyBlue, 32, 32, 576, 210);
                    using var font = new Font("Segoe UI", 20);
                    graphics.DrawString("SYNTHETIC RESEARCH CAPTURE / x = 42", font, Brushes.White, 32, 270);
                }
                var settings = new TaskSettings { AfterCaptureJob = AfterCaptureTasks.SaveResearchReceipt,
                    OverrideScreenshotsFolder = true, ScreenshotsFolder = root };
                settings.ImageSettings.ImageFormat = EImageFormat.JPEG;
                settings.ImageSettings.ImageJPEGQuality = 40;
                settings.ImageSettings.ImageAutoUseJPEG = false;
                Program.DefaultTaskSettings = settings;
                using var worker = WorkerTask.CreateImageUploaderTask(new TaskMetadata { Image = new Bitmap(original) }, settings, "synthetic-result");
                var method = typeof(WorkerTask).GetMethod("DoThreadJob", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (!(bool)method.Invoke(worker, null)) throw new InvalidDataException("WorkerTask cancelled synthetic processing.");
                string finalFile = worker.Info.FilePath;
                var link = JsonSerializer.Deserialize<ReceiptLink>(File.ReadAllBytes(finalFile + ".receipt-link.json"), ReceiptStore.Json);
                var receipt = new ReceiptWriteResult(Path.Combine(root, "Receipts", link.ReceiptId), link.ReceiptSha256);
                var verification = BitmapReceipt.Verify(receipt.Directory, receipt.ReceiptSha256);
                if (!verification.Valid) throw new InvalidDataException("Synthetic integration receipt failed verification.");
                var manifest = ReceiptStore.ReadManifest(receipt.Directory);
                if (manifest.Artifacts[0].Pixels != BitmapReceipt.Pixels(original)) throw new InvalidDataException("Worker input pixels changed.");
                if (manifest.Artifacts[0].Pixels == manifest.Artifacts[1].Pixels) throw new InvalidDataException("JPEG fixture did not exercise a pixel change.");
                using (var form = new ResearchReceiptForm(receipt.Directory, receipt.ReceiptSha256))
                {
                    form.Show(); Application.DoEvents();
                    form.SaveSanitizedPreview(Path.Combine(root, "inspector.png"));
                    form.Close();
                }
                File.WriteAllText(Path.Combine(root, "integration-result.json"), JsonSerializer.Serialize(new {
                    success = true, executable = "modified ShareX", mode = "actual WorkerTask.DoThreadJob on synthetic bitmap, JPEG40, rendered inspector; no OS screen capture", 
                    originalPixelsPreserved = true, lossyResultPixelsDiffer = true,
                    receiptDirectory = Path.GetFileName(receipt.Directory), receiptSha256 = receipt.ReceiptSha256,
                    afterCaptureFlag = ((int)AfterCaptureTasks.SaveResearchReceipt), verification
                }, ReceiptStore.Json));
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(args[1]);
                File.WriteAllText(Path.Combine(args[1], "integration-error.txt"), ex.ToString());
                Environment.ExitCode = 1;
            }
            return true;
        }
    }
}
