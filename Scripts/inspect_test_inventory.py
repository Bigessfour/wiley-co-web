#!/usr/bin/env python3

from __future__ import annotations

import argparse
import subprocess
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


@dataclass(frozen=True)
class TestPlatform:
    name: str
    project: Path
    results_folder: str


PLATFORMS = (
    TestPlatform(
        "Component",
        Path("tests/WileyCoWeb.ComponentTests/WileyCoWeb.ComponentTests.csproj"),
        "Component",
    ),
    TestPlatform(
        "Integration",
        Path("tests/WileyCoWeb.IntegrationTests/WileyCoWeb.IntegrationTests.csproj"),
        "Integration",
    ),
    TestPlatform(
        "E2E", Path("tests/WileyCoWeb.E2ETests/WileyCoWeb.E2ETests.csproj"), "E2E"
    ),
    TestPlatform(
        "Widget", Path("tests/WileyWidget.Tests/WileyWidget.Tests.csproj"), "Widget"
    ),
)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Inventory discovered tests and report the latest coverage by platform."
    )
    parser.add_argument(
        "--root",
        default=Path(__file__).resolve().parents[1],
        type=Path,
        help="Repository root",
    )
    parser.add_argument(
        "--threshold",
        default=80.0,
        type=float,
        help="Minimum desired line coverage percentage",
    )
    args = parser.parse_args()

    root = args.root.resolve()
    any_below_threshold = False

    print("Test Inventory")
    print("==============")

    for platform in PLATFORMS:
        project_path = (root / platform.project).resolve()
        discovered_tests = list_discovered_tests(project_path)
        coverage = read_latest_coverage(root, platform.results_folder)

        print(f"\n{platform.name}")
        print(f"Project: {project_path.relative_to(root)}")
        print(f"Discovered tests: {len(discovered_tests)}")
        if discovered_tests:
            print(f"First test: {discovered_tests[0]}")
            print(f"Last test: {discovered_tests[-1]}")

        if coverage is None:
            print("Coverage: unavailable")
            any_below_threshold = True
        else:
            print(f"Coverage: {coverage:.1f}%")
            if coverage < args.threshold:
                any_below_threshold = True

    if any_below_threshold:
        print(
            f"\nAction needed: one or more platforms are below the {args.threshold:.1f}% coverage target or do not yet have a report."
        )
        return 1

    print(f"\nAll platforms meet the {args.threshold:.1f}% coverage target.")
    return 0


def list_discovered_tests(project_path: Path) -> list[str]:
    if not project_path.exists():
        return []

    command = ["dotnet", "test", str(project_path), "--no-restore", "--list-tests"]
    completed = subprocess.run(command, capture_output=True, text=True, check=False)
    output = f"{completed.stdout}\n{completed.stderr}"

    tests: list[str] = []
    reading_tests = False
    for raw_line in output.splitlines():
        line = raw_line.strip()
        if not line:
            continue
        if "The following Tests are available:" in line:
            reading_tests = True
            continue
        if reading_tests:
            if (
                line.startswith("Test Run Successful")
                or line.startswith("Test run for")
                or line.startswith("VSTest")
            ):
                break
            if "." in line:
                tests.append(line)

    return tests


def read_latest_coverage(root: Path, results_folder: str) -> float | None:
    candidates = sorted(
        (root / "TestResults" / results_folder).glob("**/coverage.cobertura.xml"),
        key=lambda path: path.stat().st_mtime,
        reverse=True,
    )
    if not candidates:
        return None

    coverage_file = candidates[0]
    try:
        tree = ET.parse(coverage_file)
        coverage_node = tree.getroot()
        line_rate = coverage_node.attrib.get("line-rate")
        if line_rate is None:
            return None
        return float(line_rate) * 100.0
    except Exception:
        return None


if __name__ == "__main__":
    raise SystemExit(main())
