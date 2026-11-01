"""Apply narrow, checked substitutions to public ShareX v21.0.0. No installed app is touched."""
import os, pathlib, shutil
root=pathlib.Path(os.environ['SHAREX_FORK_ROOT'])
project=pathlib.Path(os.environ['SHAREX_RECEIPTS_ROOT'])
def replace(relative, before, after):
 p=root/relative
 text=p.read_text(encoding='utf-8-sig')
 if after in text:return
 if text.count(before)!=1:raise RuntimeError('Unexpected source anchor: '+relative)
 p.write_text(text.replace(before,after),encoding='utf-8',newline='\r\n')
replace('Directory.Build.props','<Project>','<Project>\n  <Import Project="..\\Directory.Build.props" />')
replace('ShareX/Enums.cs','DeleteFile = 1 << 21','DeleteFile = 1 << 21,\n        [System.ComponentModel.Description("Save research receipt (original + processing evidence)")]\n        SaveResearchReceipt = 1 << 22')
replace('ShareX/ShareX.csproj','<PackageReference Include="Avalonia.Win32.Interoperability" />','<ProjectReference Include="..\\..\\src\\ShareX.Receipts.Windows\\ShareX.Receipts.Windows.csproj" />\n    <PackageReference Include="Avalonia.Win32.Interoperability" />')
replace('ShareX/WorkerTask.cs','private GenericUploader uploader;','private GenericUploader uploader;\n        private ResearchCaptureReceipt researchReceipt;')
replace('ShareX/WorkerTask.cs','            if (Info.TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.BeautifyImage))','            if (Info.TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveResearchReceipt))\n                researchReceipt = new ResearchCaptureReceipt(Image, Info.TaskSettings);\n\n            if (Info.TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.BeautifyImage))')
before='            if (Info.TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AddImageEffects))'
replace('ShareX/WorkerTask.cs',before,'            researchReceipt?.Observe("after-beautify-boundary", Image, "State after the optional beautify stage.");\n\n'+before)
replace('ShareX/WorkerTask.cs','            if (Info.TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateImage))','            researchReceipt?.Observe("after-effects-boundary", Image, "State after optional image effects.");\n\n            if (Info.TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateImage))')
replace('ShareX/WorkerTask.cs','            if (Info.TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard))','            researchReceipt?.Observe("after-annotation-boundary", Image, "State after the optional annotation stage.");\n\n            if (Info.TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard))')
replace('ShareX/WorkerTask.cs','                AfterCaptureTasks.UploadImageToHost, AfterCaptureTasks.AnalyzeImage))','                AfterCaptureTasks.UploadImageToHost, AfterCaptureTasks.AnalyzeImage, AfterCaptureTasks.SaveResearchReceipt))')
replace('ShareX/WorkerTask.cs','HasFlagAny(AfterCaptureTasks.SaveImageToFile, AfterCaptureTasks.AnalyzeImage))','HasFlagAny(AfterCaptureTasks.SaveImageToFile, AfterCaptureTasks.AnalyzeImage, AfterCaptureTasks.SaveResearchReceipt))')
replace('ShareX/WorkerTask.cs','                DoFileJobs();\n            }\n            else if (Info.Job == TaskJob.TextUpload','                DoFileJobs();\n                if (researchReceipt != null && !string.IsNullOrEmpty(Info.FilePath))\n                {\n                    var receipt = researchReceipt.Seal(Info.FilePath);\n                    DebugHelper.WriteLine("Research receipt created: " + receipt.ReceiptSha256);\n                }\n            }\n            else if (Info.Job == TaskJob.TextUpload')
replace('ShareX/Forms/MainForm.cs','            InitializeComponent();','            InitializeComponent();\n            tsmiTrayTools.DropDownItems.Add("Research receipt inspector", null, (s, e) => new ResearchReceiptForm().Show());\n            tsddbTools.DropDownItems.Add("Research receipt inspector", null, (s, e) => new ResearchReceiptForm().Show());\n            cmsTaskInfo.Items.Add(new ToolStripSeparator());\n            cmsTaskInfo.Items.Add("Verify research receipt", null, (s, e) => ResearchReceiptUi.OpenForFile(uim?.SelectedItem?.Info.FilePath));')
replace('Directory.Packages.props','Include="SSH.NET" Version="2025.1.0"','Include="SSH.NET" Version="2026.0.0"')
replace('ShareX/Program.cs','        private static void Main(string[] args)\n        {','        private static void Main(string[] args)\n        {\n            if (ResearchReceiptEntry.TryRun(args)) return;')
target=root/'ShareX'/'Receipts';target.mkdir(exist_ok=True)
for name in ['ResearchCaptureReceipt.cs','ResearchReceiptUi.cs','ResearchReceiptEntry.cs']:shutil.copyfile(project/'integration'/name,target/name)
print('Pinned ShareX source patched; installed app untouched.')
