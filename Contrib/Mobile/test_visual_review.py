"""Unit tests use synthetic pixels only; these are not native UI screenshot results."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from visual_review import approve, collect_frames, compare_images, report


class VisualReviewTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.actual = self.root / "actual"
        self.actual.mkdir()

    def frame(self, name="unit-fixture.png"):
        image = Image.new("RGBA", (10, 10), (10, 20, 30, 255))
        image.save(self.actual / name)
        record = dict(engine="avalonia-headless-skia", contentKind="synthetic-parser-unit-test", image=name,
                      width=10, height=10, sha256=hashlib.sha256((self.actual / name).read_bytes()).hexdigest())
        (self.actual / (name[:-4] + ".frame.json")).write_text(json.dumps(record))
        return record

    def test_identical_pixels_pass(self):
        image = Image.new("RGB", (10, 10), "white")
        result, _ = compare_images(image, image)
        self.assertEqual("passed", result.status)
        self.assertEqual(0, result.changed_fraction)
        self.assertIsNone(result.bounds)

    def test_one_changed_pixel_has_exact_fraction_and_bounds(self):
        expected = Image.new("RGB", (10, 10), "black")
        actual = expected.copy(); actual.putpixel((4, 5), (255, 0, 0))
        result, _ = compare_images(actual, expected)
        self.assertEqual("different", result.status)
        self.assertEqual(0.01, result.changed_fraction)
        self.assertEqual((4, 5, 5, 6), result.bounds)
        self.assertEqual(255, result.maximum_delta)

    def test_low_channel_noise_can_be_tolerated(self):
        actual = Image.new("RGB", (10, 10), (4, 4, 4))
        expected = Image.new("RGB", (10, 10), (0, 0, 0))
        result, _ = compare_images(actual, expected, maximum_mean_error=4)
        self.assertEqual("passed", result.status)
        self.assertEqual(0, result.changed_fraction)

    def test_mean_error_detects_widespread_subthreshold_drift(self):
        actual = Image.new("RGB", (10, 10), (4, 4, 4))
        expected = Image.new("RGB", (10, 10), (0, 0, 0))
        result, _ = compare_images(actual, expected)
        self.assertEqual("different", result.status)
        self.assertEqual(0, result.changed_fraction)
        self.assertEqual(3, result.mean_error)

    def test_alpha_changes_are_included(self):
        actual = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
        expected = Image.new("RGBA", (10, 10), (0, 0, 0, 255))
        result, _ = compare_images(actual, expected)
        self.assertEqual(1, result.changed_fraction)
        self.assertEqual("different", result.status)

    def test_different_dimensions_are_never_rescaled(self):
        result, _ = compare_images(Image.new("RGB", (10, 10)), Image.new("RGB", (11, 10)))
        self.assertEqual("dimension-mismatch", result.status)

    def test_invalid_thresholds_are_rejected(self):
        image = Image.new("RGB", (10, 10))
        for kwargs in [dict(pixel_tolerance=-1), dict(pixel_tolerance=256), dict(maximum_changed_fraction=-1),
                       dict(maximum_changed_fraction=float("nan")), dict(maximum_mean_error=float("inf"))]:
            with self.subTest(kwargs=kwargs), self.assertRaises(ValueError):
                compare_images(image, image, **kwargs)

    def test_empty_run_is_an_error_not_a_pass(self):
        with self.assertRaisesRegex(ValueError, "No native frame"):
            report(self.actual, self.root / "report")

    def test_manifest_and_image_hash_must_agree(self):
        self.frame(); (self.actual / "unit-fixture.png").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            collect_frames(self.actual)

    def test_wrong_engine_is_rejected(self):
        record = self.frame(); record["engine"] = "html-browser"
        (self.actual / "unit-fixture.frame.json").write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError, "not marked"):
            collect_frames(self.actual)

    def test_path_traversal_is_rejected(self):
        record = self.frame(); record["image"] = "../unit-fixture.png"
        (self.actual / "unit-fixture.frame.json").write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError, "Invalid"):
            collect_frames(self.actual)

    def test_manifest_dimensions_are_verified(self):
        record = self.frame(); record["width"] = 99
        (self.actual / "unit-fixture.frame.json").write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError, "dimensions"):
            collect_frames(self.actual)

    def test_missing_baseline_is_unreviewed_and_fails_by_default(self):
        self.frame(); result = report(self.actual, self.root / "report")
        self.assertEqual("unreviewed", result["status"])
        self.assertFalse(result["gatePassed"])
        self.assertTrue((self.root / "report" / "index.html").is_file())

    def test_opt_in_render_only_still_reports_unreviewed(self):
        self.frame(); result = report(self.actual, self.root / "report", allow_unreviewed=True)
        self.assertEqual("unreviewed", result["status"])
        self.assertTrue(result["gatePassed"])

    def test_approval_requires_explicit_review(self):
        self.frame()
        with self.assertRaisesRegex(ValueError, "--reviewed"):
            approve(self.actual, self.root / "baseline", reviewed=False)

    def test_existing_baseline_is_not_overwritten_implicitly(self):
        self.frame(); baseline = self.root / "baseline"
        self.assertEqual(1, approve(self.actual, baseline, reviewed=True))
        with self.assertRaisesRegex(ValueError, "protected"):
            approve(self.actual, baseline, reviewed=True)

    def test_approved_identical_frames_pass_and_create_difference_evidence(self):
        self.frame(); baseline = self.root / "baseline"
        approve(self.actual, baseline, reviewed=True)
        result = report(self.actual, self.root / "report", baseline)
        self.assertEqual("passed", result["status"])
        self.assertTrue(result["gatePassed"])
        self.assertTrue((self.root / "report" / "diff" / "unit-fixture.png").is_file())

    def test_new_case_missing_from_baseline_is_not_a_silent_pass(self):
        self.frame(); baseline = self.root / "baseline"
        approve(self.actual, baseline, reviewed=True); self.frame("new-case.png")
        result = report(self.actual, self.root / "report", baseline)
        self.assertEqual(1, result["unreviewed"])
        self.assertFalse(result["gatePassed"])

    def test_unreviewed_png_is_not_an_approved_baseline(self):
        self.frame(); baseline = self.root / "baseline"; baseline.mkdir()
        (baseline / "unit-fixture.png").write_bytes((self.actual / "unit-fixture.png").read_bytes())
        result = report(self.actual, self.root / "report", baseline)
        self.assertFalse(result["gatePassed"])
        self.assertEqual("unreviewed-baseline", result["frames"][0]["status"])

    def test_reviewed_baseline_hash_cannot_change_silently(self):
        self.frame(); baseline = self.root / "baseline"
        approve(self.actual, baseline, reviewed=True)
        Image.new("RGB", (10, 10), "white").save(baseline / "unit-fixture.png")
        with self.assertRaisesRegex(ValueError, "baseline hash mismatch"):
            report(self.actual, self.root / "report", baseline)

    def test_non_png_capture_is_rejected_even_with_matching_manifest(self):
        record = self.frame()
        Image.new("RGB", (10, 10), "white").save(self.actual / "unit-fixture.png", format="JPEG")
        record["sha256"] = hashlib.sha256((self.actual / "unit-fixture.png").read_bytes()).hexdigest()
        (self.actual / "unit-fixture.frame.json").write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError, "not a PNG"):
            collect_frames(self.actual)

    def test_approving_new_cases_preserves_previous_approval_records(self):
        self.frame(); baseline = self.root / "baseline"
        approve(self.actual, baseline, reviewed=True)
        (self.actual / "unit-fixture.frame.json").unlink()
        (self.actual / "unit-fixture.png").unlink()
        self.frame("new-case.png")
        approve(self.actual, baseline, reviewed=True)
        approval = json.loads((baseline / "approval.json").read_text())
        self.assertEqual({"unit-fixture.png", "new-case.png"}, {frame["image"] for frame in approval["frames"]})


if __name__ == "__main__":
    unittest.main()
