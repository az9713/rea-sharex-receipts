// SPDX-License-Identifier: GPL-3.0-or-later
using System.Drawing;
using System.Text.Json;
using ShareX.Receipts;

try
{
    if (args.Length >= 2 && args[0] == "verify")
    {
        var result = BitmapReceipt.Verify(args[1], args.Length == 3 ? args[2] : null);
        Console.WriteLine(JsonSerializer.Serialize(result, ReceiptStore.Json));
        return result.Valid ? 0 : 2;
    }
    if (args.Length == 2 && args[0] == "demo")
    {
        using var original = new Bitmap(320, 180);
        using (var g = Graphics.FromImage(original))
        { g.Clear(Color.FromArgb(17, 30, 46)); g.FillRectangle(Brushes.CornflowerBlue, 20, 25, 150, 90); g.DrawString("SYNTHETIC CAPTURE / x = 42", SystemFonts.DefaultFont, Brushes.White, 24, 135); }
        using var processed = (Bitmap)original.Clone();
        using (var g = Graphics.FromImage(processed)) g.DrawRectangle(Pens.Gold, 17, 22, 156, 96);
        var recipe = new CaptureRecipe("synthetic input bitmap / before after-capture processing", ["AnnotateImage"],
            new() { ["outputFormat"] = "PNG" },
            [new("input", "observed", BitmapReceipt.Pixels(original), "Original in-memory bitmap"),
             new("annotation", "observed", BitmapReceipt.Pixels(processed), "Gold rectangle in synthetic fixture")],
            "Synthetic demonstration", "No screen, window, user document, OCR or upload was used.");
        var result = ReceiptStore.Create(args[1], BitmapReceipt.Png(original), BitmapReceipt.Pixels(original),
            BitmapReceipt.Png(processed), "png", "image/png", BitmapReceipt.Pixels(processed), recipe);
        Console.WriteLine(JsonSerializer.Serialize(result, ReceiptStore.Json));
        return 0;
    }
    Console.WriteLine("ShareX Receipts\n  receipt demo <new-receipt-parent>\n  receipt verify <receipt-folder> [independently-retained-receipt-sha256]\nVerification proves byte/pixel integrity against commitments, not origin authenticity.");
    return args.Length == 0 ? 0 : 64;
}
catch (Exception ex)
{ Console.Error.WriteLine(ex.Message); return 1; }
