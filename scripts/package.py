"""Package only explicit public sources and known publish outputs. Never scan outside this checkout."""
import hashlib,json,pathlib,subprocess,zipfile
from release_privacy import scan
root=pathlib.Path(__file__).resolve().parent.parent
out=root/'releases';out.mkdir(exist_ok=True)
public=json.loads((root/'public-files.json').read_text(encoding='utf-8'))['files']
for relative in public:
 p=root/relative
 if relative=='integration/sharex-v21.0.0.patch':continue # regenerated below
 if not p.is_file() or p.is_symlink() or p.is_junction() or not p.resolve().is_relative_to(root) or p.name.endswith('.private.json'):
  raise RuntimeError('Invalid explicit public file: '+relative)
def files(path):
 if not path.resolve().is_relative_to(root):raise RuntimeError('Input escaped project')
 if path.is_symlink() or path.is_junction():raise RuntimeError('Linked input rejected')
 if path.is_file():yield path;return
 if path.exists():
  for p in sorted(path.rglob('*')):
   if p.is_symlink() or p.is_junction() or not p.resolve().is_relative_to(root):raise RuntimeError('Linked or escaped package input')
   if p.is_file() and not any(x in {'bin','obj','.git','__pycache__'} for x in p.relative_to(path).parts) and not p.name.endswith('.private.json') and p.suffix!='.pdb':yield p
sample_allow={'original.png','result.jpg','recipe.json','receipt.json','receipt.sha256'}
for relative in ['docs/sample','fixtures/sample']:
 sample=root/relative
 if not sample.is_dir() or {p.name for p in sample.iterdir()} != sample_allow:raise RuntimeError('Sample inputs differ from explicit five-file allowlist')
assets=[]
for directory in ['ShareX-Receipts-v0.1.1-win-x64','Receipt-CLI-v0.1.1-win-x64']:
 folder=out/directory
 if not folder.exists():raise RuntimeError('Publish before packaging: '+directory)
 privacy=scan(folder,root)
 if privacy['matches']:raise RuntimeError('Private build paths remain: '+str(privacy['matches']))
 target=out/(directory+'.zip')
 if target.exists():raise RuntimeError('Refusing to overwrite existing release archive: '+target.name)
 with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for p in files(folder):z.write(p,str(pathlib.Path(directory)/p.relative_to(folder)))
 assets.append(target)
patch=subprocess.check_output(['git','-C',str(root/'upstream'),'diff','--binary'],cwd=root)
(root/'integration'/'sharex-v21.0.0.patch').write_bytes(patch)
target=out/'ShareX-Receipts-v0.1.1-corresponding-source.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for relative in public:
  for p in files(root/relative):z.write(p,str(p.relative_to(root)))
 upstream_files=subprocess.check_output(['git','-C',str(root/'upstream'),'ls-files','-z'],cwd=root).decode('utf-8').split('\0')
 upstream_files.extend('ShareX/Receipts/'+n for n in ['ResearchCaptureReceipt.cs','ResearchReceiptUi.cs','ResearchReceiptEntry.cs'])
 for relative in sorted(set(x for x in upstream_files if x)):
  p=root/'upstream'/relative
  if not p.is_file() or not p.resolve().is_relative_to(root/'upstream') or p.is_symlink():raise RuntimeError('Invalid upstream public source file')
  z.write(p,str(p.relative_to(root)))
assets.append(target)
manifest=[{'file':p.name,'bytes':p.stat().st_size,'sha256':hashlib.file_digest(p.open('rb'),'sha256').hexdigest()} for p in assets]
(out/'SHA256SUMS-v0.1.1.txt').write_text(''.join(f"{m['sha256']}  {m['file']}\n" for m in manifest),encoding='ascii')
(out/'release-assets-v0.1.1.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(manifest,indent=2))
