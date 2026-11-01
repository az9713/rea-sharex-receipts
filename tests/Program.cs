// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShareX.Receipts;

string root = Path.GetFullPath(args[0]);
Directory.CreateDirectory(root);
var results = new List<object>();
int passed = 0, failed = 0;
void Test(string name, Action action) {
 try { action(); results.Add(new {name, passed=true}); passed++; }
 catch(Exception e) { results.Add(new{name,passed=false,error=e.Message}); failed++; }
}
void Assert(bool condition, string detail) { if(!condition)throw new Exception(detail); }
using var image = new Bitmap(2, 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
image.SetPixel(0,0,Color.Red); image.SetPixel(1,0,Color.FromArgb(128,0,255,0));
image.SetPixel(0,1,Color.Blue); image.SetPixel(1,1,Color.White);
byte[] png = BitmapReceipt.Png(image);
PixelIdentity pixels = BitmapReceipt.Pixels(image);
var recipe = new CaptureRecipe("synthetic worker input", ["SaveImageToFile","UploadImageToHost"], new(){["fixture"]="synthetic"},
 [new("worker-input","observed",pixels,"No OS capture")], "synthetic only", "Requested upload was not executed.");
ReceiptWriteResult Make(string id) => ReceiptStore.Create(root,png,pixels,png,"png","image/png",pixels,recipe,DateTimeOffset.Parse("2026-10-31T00:00:00Z"),id);
void Rewrite(string dir,Action<JsonObject> mutate) {
 string path=Path.Combine(dir,"receipt.json"); var node=JsonNode.Parse(File.ReadAllText(path))!.AsObject(); mutate(node);
 byte[] bytes=Encoding.UTF8.GetBytes(node.ToJsonString(ReceiptStore.Json)); File.WriteAllBytes(path,bytes);
 File.WriteAllText(Path.Combine(dir,"receipt.sha256"),ReceiptStore.Hash(bytes)+"  receipt.json\n");
}
Test("canonical RGBA includes alpha, row order and dimensions",()=>{
 byte[] domain=Encoding.ASCII.GetBytes("sharex-pixels-v1\n"); byte[] bytes=new byte[domain.Length+8+16]; domain.CopyTo(bytes,0);
 BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(domain.Length),2); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(domain.Length+4),2);
 byte[] rgba=[255,0,0,255,0,255,0,128,0,0,255,255,255,255,255,255]; rgba.CopyTo(bytes,domain.Length+8);
 Assert(pixels.Sha256==ReceiptStore.Hash(bytes),"Canonical encoding differed");
});
Test("PNG roundtrip preserves canonical input pixels",()=>{ using var stream=new MemoryStream(png); using var decoded=new Bitmap(stream); Assert(BitmapReceipt.Pixels(decoded)==pixels,"PNG pixels changed"); });
Test("valid receipt verifies bytes and decoded pixels",()=>{var r=Make("valid");Assert(BitmapReceipt.Verify(r.Directory,r.ReceiptSha256).Valid,"Valid failed");});
foreach(string file in new[]{"original.png","result.png","recipe.json","receipt.json"}) Test("tampering detected: "+file,()=>{
 var r=Make("tamper-"+file.Replace('.','-')); File.AppendAllText(Path.Combine(r.Directory,file),"x"); Assert(!BitmapReceipt.Verify(r.Directory).Valid,"Tamper passed");
});
Test("missing artifact rejected",()=>{var r=Make("missing");File.Delete(Path.Combine(r.Directory,"result.png"));Assert(!ReceiptStore.Verify(r.Directory).Valid,"Missing passed");});
Test("uncommitted extra entry rejected",()=>{var r=Make("extra");File.WriteAllText(Path.Combine(r.Directory,"extra.txt"),"x");Assert(!ReceiptStore.Verify(r.Directory).Valid,"Extra passed");});
Test("path traversal rejected before artifact reads",()=>{var r=Make("traversal");Rewrite(r.Directory,n=>n["artifacts"]![1]!["name"]="result.../escape");Assert(!ReceiptStore.Verify(r.Directory).Valid,"Traversal passed");});
Test("unsupported schema rejected",()=>{var r=Make("schema");Rewrite(r.Directory,n=>n["schema"]="future/2");Assert(!ReceiptStore.Verify(r.Directory).Valid,"Unknown schema passed");});
Test("pixel mismatch rejected after byte-level recommit",()=>{var r=Make("pixels");Rewrite(r.Directory,n=>n["artifacts"]![0]!["pixels"]!["sha256"]=new string('0',64));Assert(ReceiptStore.Verify(r.Directory).Valid,"Byte commitment failed");Assert(!BitmapReceipt.Verify(r.Directory).Valid,"False pixel commitment passed");});
Test("independent anchor detects fully rewritten receipt",()=>{var r=Make("anchor");Rewrite(r.Directory,n=>n["producer"]="rewritten");Assert(ReceiptStore.Verify(r.Directory).Valid,"Companion should admit rewritten document");Assert(!ReceiptStore.Verify(r.Directory,r.ReceiptSha256).Valid,"Trusted digest missed rewrite");});
Test("existing immutable receipt cannot be overwritten",()=>{Make("immutable");bool rejected=false;try{Make("immutable");}catch(IOException){rejected=true;}Assert(rejected,"Overwrite allowed");});
Test("requested tasks stay distinct from observed stages",()=>{var r=Make("semantics");var loaded=JsonSerializer.Deserialize<CaptureRecipe>(File.ReadAllText(Path.Combine(r.Directory,"recipe.json")),ReceiptStore.Json)!;Assert(loaded.RequestedTasks.Contains("UploadImageToHost")&&loaded.ObservedStages.All(s=>!s.Name.Contains("upload")),"Conflated intent with observation");});
File.WriteAllText(Path.Combine(root,"test-results.json"),JsonSerializer.Serialize(new {passed,failed,results},ReceiptStore.Json));
Console.WriteLine($"{passed} passed; {failed} failed"); Environment.ExitCode=failed==0?0:1;
