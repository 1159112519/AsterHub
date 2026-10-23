import pathlib, unittest, xml.etree.ElementTree as ET
ROOT = pathlib.Path(__file__).resolve().parents[1]
NS = {"m": "http://schemas.microsoft.com/developer/msbuild/2003"}
class WindowsStructureTests(unittest.TestCase):
    def test_portable_projects_do_not_depend_on_desktop_or_windows(self):
        for module in ["AQServer.Core", "AQServer.Storage"]:
            project = ROOT / "desktop/src" / module / (module + ".csproj")
            self.assertTrue(project.exists(), str(project))
            tree = ET.parse(project)
            references = [e.attrib["Include"] for e in tree.findall(".//m:Reference", NS)]
            self.assertFalse(set(references) & {"PresentationFramework", "System.Management", "System.Windows.Forms"})
            for source in project.parent.rglob("*.cs"):
                text = source.read_text(encoding="utf-8-sig")
                self.assertNotIn("using System.Windows", text, str(source))
                self.assertNotIn("DllImport(", text, str(source))
    def test_bootstrap_probes_only_private_installation_directories(self):
        config = ROOT / "desktop/src/AQServer.Bootstrap/App.config"
        self.assertTrue(config.exists())
        tree = ET.parse(config)
        probing = tree.find(".//{urn:schemas-microsoft-com:asm.v1}probing")
        paths = probing.attrib["privatePath"].split(";")
        self.assertEqual(set(paths), {"app", "app/modules", "runtime/managed"})
        self.assertTrue(all(not p.startswith("/") and ".." not in p for p in paths))
    def test_solution_contains_real_module_projects(self):
        solution = ROOT / "desktop/AQServer.sln"
        self.assertTrue(solution.exists())
        text = solution.read_text()
        for name in ["AQServer.Core", "AQServer.Storage", "AQServer.Platform.Windows", "AQServer.Desktop", "AQServer.Bootstrap"]:
            self.assertIn(name + ".csproj", text)
if __name__ == "__main__":
    unittest.main()
