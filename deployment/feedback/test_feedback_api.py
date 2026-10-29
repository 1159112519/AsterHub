import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("feedback_api", Path(__file__).with_name("feedback_api.py"))
api = importlib.util.module_from_spec(spec)
spec.loader.exec_module(api)

class FeedbackTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.root = Path(self.folder.name)
        self.store = api.FeedbackStore(self.root, rate_limit=2)
        self.payload = {"schemaVersion": 1, "appVersion": "0.2.1", "platform": "Windows", "category": "bug", "title": "界面问题", "description": "打开历史后点击时间点，出现一次抖动。"}
    def tearDown(self):
        self.folder.cleanup()
    def test_preview_version_is_preserved(self):
        self.payload["appVersion"] = "0.3.0-preview.4"
        receipt = self.store.accept(self.payload, "192.0.2.1")
        saved = json.loads(next(self.root.rglob("*.json")).read_text(encoding="utf-8"))
        self.assertEqual(saved["appVersion"], "0.3.0-preview.4")
        self.assertEqual(saved["id"], receipt["id"])
    def test_unsupported_or_malformed_versions_rejected(self):
        for version in ["0.3.0-preview", "0.3.0-preview.-1", "0.3.0-preview.4-extra", "0.3.0-beta.1", "1.2.3.4", "0.3.0-preview.\n4"]:
            with self.subTest(version=version):
                self.payload["appVersion"] = version
                with self.assertRaises(api.FeedbackError): self.store.validate(self.payload)
        self.assertEqual(list(self.root.rglob("*.json")), [])
    def test_receipt_and_private_storage(self):
        result = self.store.accept(self.payload, "192.0.2.1")
        files = list(self.root.rglob("*.json"))
        self.assertEqual(len(files), 1)
        saved = json.loads(files[0].read_text(encoding="utf-8"))
        self.assertEqual(result["id"], saved["id"])
        self.assertEqual(saved["description"], self.payload["description"])
        self.assertNotIn("ip", saved)
        self.assertNotIn("environment", saved)
        self.assertNotIn("192.0.2.1", files[0].read_text(encoding="utf-8"))
    def test_required_text_and_no_side_effect(self):
        self.payload["description"] = "  "
        with self.assertRaises(api.FeedbackError): self.store.accept(self.payload, "192.0.2.1")
        self.assertEqual(list(self.root.rglob("*.json")), [])
    def test_extra_private_fields_rejected(self):
        self.payload["servers"] = [{"password": "must-not-be-saved"}]
        with self.assertRaises(api.FeedbackError): self.store.accept(self.payload, "192.0.2.1")
        self.assertEqual(list(self.root.rglob("*.json")), [])
    def test_rate_limit(self):
        self.store.accept(self.payload, "192.0.2.1")
        self.store.accept(self.payload, "192.0.2.1")
        with self.assertRaises(api.FeedbackError) as caught: self.store.accept(self.payload, "192.0.2.1")
        self.assertEqual(caught.exception.status, 429)
        self.store.accept(self.payload, "192.0.2.2")
    def test_client_text_does_not_choose_file_path(self):
        self.payload["title"] = "../../escape.json"
        self.store.accept(self.payload, "192.0.2.1")
        self.assertEqual(len(list(self.root.rglob("FB-*.json"))), 1)
        self.assertFalse((self.root / "escape.json").exists())
    def test_capacity_refusal_keeps_prior_data(self):
        self.store.max_files = 1
        self.store.accept(self.payload, "192.0.2.1")
        original = next(self.root.rglob("*.json")).read_bytes()
        with self.assertRaises(api.FeedbackError) as caught: self.store.accept(self.payload, "192.0.2.2")
        self.assertEqual(caught.exception.status, 503)
        self.assertEqual(next(self.root.rglob("*.json")).read_bytes(), original)
    def test_invalid_version_and_category(self):
        for key, value in [("appVersion", "../x"), ("category", "execute")]:
            invalid = dict(self.payload); invalid[key] = value
            with self.assertRaises(api.FeedbackError): self.store.accept(invalid, "192.0.2.1")
    def test_optional_information_is_explicit(self):
        self.payload.update(contact=" user@example.test ", environment="Windows test environment")
        self.store.accept(self.payload, "192.0.2.1")
        saved = json.loads(next(self.root.rglob("*.json")).read_text(encoding="utf-8"))
        self.assertEqual(saved["contact"], "user@example.test")

if __name__ == "__main__": unittest.main()
