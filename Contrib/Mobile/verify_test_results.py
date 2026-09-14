#!/usr/bin/env python3
"""Reject empty, incomplete, failed, or missing native test runs."""
from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def verify(path: Path) -> int:
    root = ET.parse(path).getroot()
    namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    summary = root.find("t:ResultSummary", namespace)
    counters = root.find("t:ResultSummary/t:Counters", namespace)
    if summary is None or counters is None:
        raise ValueError("TRX has no test result summary")
    total = int(counters.get("total", "0"))
    executed = int(counters.get("executed", "0"))
    passed = int(counters.get("passed", "0"))
    if total <= 0 or executed != total or passed != executed:
        raise ValueError(f"Native tests did not pass: {passed}/{executed} executed; {total} discovered")
    if summary.get("outcome") != "Completed":
        raise ValueError(f"Native test run did not complete: {summary.get('outcome')}")
    for name in ("failed", "error", "timeout", "aborted", "disconnected"):
        if int(counters.get(name, "0")):
            raise ValueError(f"Native test run contains {name} results")
    results = root.findall("t:Results/t:UnitTestResult", namespace)
    if len(results) != executed or any(result.get("outcome") != "Passed" for result in results):
        raise ValueError("Native test results disagree with successful execution counters")
    return executed


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trx", type=Path)
    args = parser.parse_args()
    try:
        count = verify(args.trx)
    except (OSError, ET.ParseError, ValueError) as error:
        print(f"Native validation failed: {error}", file=sys.stderr)
        return 1
    print(f"Verified non-empty native test run: {count} passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
