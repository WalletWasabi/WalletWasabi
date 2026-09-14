"""Gate self-tests use synthetic files, never purported wallet screenshots."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET
from PIL import Image
from verify_repeatability import verify as repeat
from verify_test_results import verify as tests


class RepeatabilityTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.first, self.second = self.root / "first", self.root / "second"
        self.first.mkdir(); self.second.mkdir()
        self.output = self.root / "result.json"
        for path in (self.first, self.second):
            self.frame(path)

    @staticmethod
    def frame(path, value=10, theme="Dark", name="test"):
        image = Image.new("RGBA", (4, 4), (value, 20, 30, 255))
        image.save(path / (name + ".png"))
        record = dict(engine="avalonia-headless-skia", image=name + ".png", width=4, height=4,
                      theme=theme, sha256=hashlib.sha256((path / (name + ".png")).read_bytes()).hexdigest())
        (path / (name + ".frame.json")).write_text(json.dumps(record))

    def test_identical_independent_frames_pass(self):
        self.assertTrue(repeat(self.first, self.second, self.output)["gatePassed"])

    def test_even_one_channel_of_drift_fails(self):
        self.frame(self.second, value=11)
        self.assertFalse(repeat(self.first, self.second, self.output)["gatePassed"])

    def test_same_pixels_under_different_context_fail(self):
        self.frame(self.second, theme="Light")
        self.assertEqual("context-drift", repeat(self.first, self.second, self.output)["differences"][0]["reason"])

    def test_missing_case_fails(self):
        self.frame(self.first, name="extra")
        self.assertFalse(repeat(self.first, self.second, self.output)["gatePassed"])

    def test_same_directory_is_not_an_independent_run(self):
        with self.assertRaisesRegex(ValueError, "independent"):
            repeat(self.first, self.first, self.output)


class TestResultTests(unittest.TestCase):
    def trx(self, total=2, executed=2, passed=2, outcomes=("Passed", "Passed")):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        path = Path(temp.name) / "result.trx"
        root = ET.Element("TestRun", xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
        results = ET.SubElement(root, "Results")
        for outcome in outcomes:
            ET.SubElement(results, "UnitTestResult", outcome=outcome)
        summary = ET.SubElement(root, "ResultSummary", outcome="Completed")
        ET.SubElement(summary, "Counters", total=str(total), executed=str(executed), passed=str(passed))
        ET.ElementTree(root).write(path)
        return path

    def test_all_executed_and_passed(self):
        self.assertEqual(2, tests(self.trx()))

    def test_skipped_cases_fail_closed(self):
        with self.assertRaises(ValueError):
            tests(self.trx(total=3))

    def test_forged_counters_without_results_fail(self):
        with self.assertRaises(ValueError):
            tests(self.trx(outcomes=()))

    def test_failed_result_cannot_hide_behind_success_counters(self):
        with self.assertRaises(ValueError):
            tests(self.trx(outcomes=("Passed", "Failed")))


class AotDescriptorTests(unittest.TestCase):
    def test_host_roots_are_isolated(self):
        root = Path(__file__).resolve().parents[2]
        hosts = {"WalletWasabi.Fluent.Desktop", "WalletWasabi.Fluent.Android", "WalletWasabi.Fluent.iOS"}
        shared = {node.get("fullname") for node in ET.parse(root / "Roots.xml").findall("assembly")}
        self.assertFalse(shared & hosts)
        for host in hosts:
            with self.subTest(host=host):
                entries = ET.parse(root / "Contrib/Aot" / (host + ".xml")).findall("assembly")
                self.assertEqual([host], [node.get("fullname") for node in entries])
        ios = ET.parse(root / "Contrib/Aot/WalletWasabi.Fluent.iOS.xml")
        self.assertEqual({"WalletWasabi.Fluent.IOS.Program", "WalletWasabi.Fluent.IOS.AppDelegate"},
                         {node.get("fullname") for node in ios.findall("assembly/type")})
        props = ET.parse(root / "NativeAot.props")
        self.assertTrue(any("Contrib/Aot/$(MSBuildProjectName).xml" in node.get("Include", "")
                            for node in props.iter("TrimmerRootDescriptor")))


if __name__ == "__main__":
    unittest.main()
