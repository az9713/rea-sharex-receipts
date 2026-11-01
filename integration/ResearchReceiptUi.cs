// SPDX-License-Identifier: GPL-3.0-or-later
using ShareX.Receipts;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace ShareX
{
    internal static class ResearchReceiptUi
    {
        public static void OpenForFile(string file)
        {
            try
            {
                if (string.IsNullOrEmpty(file)) throw new InvalidDataException("Select a capture in the task list first.");
                string sidecar = file + ".receipt-link.json";
                if (!File.Exists(sidecar)) throw new InvalidDataException("This capture has no receipt link. Enable 'Save research receipt' before capturing.");
                if (new FileInfo(sidecar).Length > 4096) throw new InvalidDataException("Receipt link exceeds 4 KiB.");
                var link = JsonSerializer.Deserialize<ReceiptLink>(File.ReadAllBytes(sidecar), ReceiptStore.Json);
                if (link == null || link.ReceiptId.Length != 32 || link.ReceiptId.Any(c => !Uri.IsHexDigit(c)))
                    throw new InvalidDataException("Invalid receipt link identifier.");
                string directory = Path.Combine(Path.GetDirectoryName(file), "Receipts", link.ReceiptId);
                new ResearchReceiptForm(directory, link.ReceiptSha256).Show();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "ShareX Receipts", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }
    }

    internal sealed class ResearchReceiptForm : Form
    {
        private readonly TextBox folder = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox anchor = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Optional independently retained receipt SHA-256" };
        private readonly TextBox report = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, WordWrap = false };
        private readonly PictureBox preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        private readonly Label status = new Label { Dock = DockStyle.Fill, Text = "Select a receipt folder. Verification is integrity checking, not proof of origin.", AutoSize = true };

        public ResearchReceiptForm(string initialFolder = null, string expected = null)
        {
            Text = "ShareX Receipts — original pixels and processing evidence";
            Width = 1080; Height = 760; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(17, 29, 43); ForeColor = Color.Gainsboro;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(16) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 33)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var browse = new Button { Text = "Choose receipt…", AutoSize = true };
            var verify = new Button { Text = "Verify hashes + pixels", AutoSize = true };
            var original = new Button { Text = "Show original", AutoSize = true };
            var result = new Button { Text = "Show result", AutoSize = true };
            browse.Click += (s, e) => { using (var dialog = new FolderBrowserDialog()) if (dialog.ShowDialog(this) == DialogResult.OK) { folder.Text = dialog.SelectedPath; anchor.Clear(); Verify(); } };
            verify.Click += (s, e) => Verify();
            original.Click += (s, e) => Preview("original.png");
            result.Click += (s, e) => { try { Preview(ReceiptStore.ReadManifest(folder.Text).Artifacts[1].Name); } catch (Exception ex) { status.Text = ex.Message; } };
            actions.Controls.AddRange(new Control[] { browse, verify, original, result });
            layout.Controls.Add(folder); layout.Controls.Add(anchor); layout.Controls.Add(actions); layout.Controls.Add(status); layout.Controls.Add(preview); layout.Controls.Add(report);
            Controls.Add(layout);
            foreach (Control c in new Control[] { folder, anchor, report }) { c.BackColor = Color.FromArgb(25, 42, 61); c.ForeColor = ForeColor; }
            FormClosed += (s, e) => preview.Image?.Dispose();
            if (!string.IsNullOrEmpty(initialFolder)) { folder.Text = initialFolder; anchor.Text = expected ?? ""; Shown += (s, e) => Verify(); }
        }

        private void Verify()
        {
            var result = BitmapReceipt.Verify(folder.Text, string.IsNullOrWhiteSpace(anchor.Text) ? null : anchor.Text.Trim());
            status.Text = result.Valid ? "VALID: committed bytes and decoded pixels match. A co-located hash is not a signature." : "FAILED: receipt is missing, altered, unsupported or inconsistent.";
            status.ForeColor = result.Valid ? Color.LightGreen : Color.Salmon;
            report.Text = JsonSerializer.Serialize(result, ReceiptStore.Json);
            if (result.Valid)
            {
                report.AppendText(Environment.NewLine + Environment.NewLine + File.ReadAllText(Path.Combine(folder.Text, "recipe.json")));
                Preview("original.png");
            }
        }

        internal void SaveSanitizedPreview(string path)
        {
            string actual = folder.Text;
            folder.Text = "Synthetic fixture / Receipts / " + Path.GetFileName(actual);
            try
            {
                using var render = new Bitmap(Width, Height);
                DrawToBitmap(render, new Rectangle(Point.Empty, Size));
                render.Save(path);
            }
            finally { folder.Text = actual; }
        }

        private void Preview(string name)
        {
            try
            {
                if (!BitmapReceipt.Verify(folder.Text, string.IsNullOrWhiteSpace(anchor.Text) ? null : anchor.Text.Trim()).Valid)
                    throw new InvalidDataException("Verify a valid receipt before previewing it.");
                using (var image = new Bitmap(Path.Combine(folder.Text, name)))
                { var old = preview.Image; preview.Image = new Bitmap(image); old?.Dispose(); }
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
    }
}
