import json, pathlib, shutil, subprocess, sys, tempfile, unittest
ROOT = pathlib.Path(__file__).resolve().parents[1]
CURRENT=(ROOT/'VERSION').read_text().strip()
NEXT='.'.join(CURRENT.split('.')[:2]+[str(int(CURRENT.split('.')[2])+1)])
class VersionToolTests(unittest.TestCase):
    def fixture(self, destination):
        files = ["VERSION", "scripts/set-version.py", "desktop/Properties/AssemblyInfo.cs", "desktop/src/AQServer.Desktop/Monitoring/MonitorWindow.cs", "website/src/App.tsx", "website/package.json", "website/package-lock.json", "deployment/deploy.sh", "installer/AQServer.iss", "linux/VERSION", "linux/aqserver/storage.py", "linux/README.md"]
        for name in files:
            path = destination / name
            path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / name, path)
        return files
    def test_version_bump_covers_modules_installer_and_website(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder); self.fixture(root)
            result = subprocess.run([sys.executable, str(root / "scripts/set-version.py"), NEXT], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual((root / "VERSION").read_text().strip(), NEXT)
            self.assertEqual((root / "linux/VERSION").read_text().strip(), NEXT)
            self.assertIn('AssemblyVersion("'+NEXT+'.0")', (root / "desktop/Properties/AssemblyInfo.cs").read_text(encoding="utf-8-sig"))
            self.assertIn('AppVersion '+chr(34)+NEXT+chr(34), (root / "installer/AQServer.iss").read_text(encoding="utf-8"))
            self.assertEqual(json.loads((root / "website/package.json").read_text())["version"], NEXT)
    def test_missing_version_anchor_does_not_partially_write(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder); files = self.fixture(root)
            (root / "installer/AQServer.iss").write_text("missing anchor")
            before = {name: (root / name).read_bytes() for name in files}
            result = subprocess.run([sys.executable, str(root / "scripts/set-version.py"), NEXT], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(before, {name: (root / name).read_bytes() for name in files})
if __name__ == "__main__": unittest.main()
