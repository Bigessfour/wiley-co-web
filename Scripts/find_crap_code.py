#!/usr/bin/env python3
"""
WileyCoWeb maintainability hotspot detector.

Scans active runtime C# / Razor code for methods worth refactoring.
CRAP = CC² × (1 - coverage)³ + CC.
"""

import argparse
import csv
import io
import re
import subprocess
import tempfile
from pathlib import Path
from typing import Dict, List, Sequence, Tuple

ACTIVE_DIRECTORIES = (
    "Components",
    "Services",
    "WileyCoWeb.Api",
    "State",
)

EXCLUDED_DIR_NAMES = {
    "__remote_check__",
    ".git",
    ".tmp",
    "bin",
    "coverage",
    "logs",
    "migrations",
    "obj",
    "properties",
    "publish_output",
    "src",
    "testresults",
    "tests",
}

EXCLUDED_FILE_SUFFIXES = (
    ".assemblyinfo.cs",
    ".designer.cs",
    ".g.cs",
    ".g.i.cs",
    ".generated.cs",
)

EXCLUDED_FILE_NAMES = {
    "appdbcontextmodelsnapshot.cs",
}

HUNK_HEADER_RE = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")


def normalize_coverage(coverage: float) -> float:
    """Accept either a 0-1 fraction or a 0-100 percentage."""
    if coverage > 1:
        coverage /= 100.0

    return max(0.0, min(1.0, coverage))


def is_noise_file(source_file: Path) -> bool:
    """Filter generated, legacy, and test paths out of the report."""
    lowered_parts = [
        part.lower()
        for part in source_file.as_posix().split("/")
        if part and part != "."
    ]

    if not lowered_parts:
        return True

    file_name = lowered_parts[-1]

    if file_name in EXCLUDED_FILE_NAMES:
        return True

    if any(file_name.endswith(suffix) for suffix in EXCLUDED_FILE_SUFFIXES):
        return True

    return any(part in EXCLUDED_DIR_NAMES for part in lowered_parts)


def collect_source_files(root_path: Path) -> List[str]:
    """Collect only active runtime source files before invoking lizard."""
    sources = set()

    # Scan only the active runtime tree; the repo root also contains legacy and generated artifacts.
    for directory_name in ACTIVE_DIRECTORIES:
        directory_path = root_path / directory_name
        if not directory_path.exists():
            continue

        for source_file in directory_path.rglob("*.cs"):
            if not is_noise_file(source_file):
                sources.add(str(source_file.resolve()))

    for source_file in root_path.glob("*.cs"):
        if not is_noise_file(source_file):
            sources.add(str(source_file.resolve()))

    return sorted(sources)


def resolve_source_files(
    root_path: Path, file_paths: Sequence[str] | None
) -> List[str]:
    """Resolve requested files, or fall back to the active runtime tree."""
    if not file_paths:
        return collect_source_files(root_path)

    resolved_files = set()

    for file_path in file_paths:
        candidate = Path(file_path)
        if not candidate.is_absolute():
            candidate = root_path / candidate

        candidate = candidate.resolve()
        if (
            candidate.suffix.lower() != ".cs"
            or is_noise_file(candidate)
            or not candidate.exists()
        ):
            continue

        resolved_files.add(str(candidate))

    return sorted(resolved_files)


def collect_changed_line_ranges(
    root_path: Path, diff_base: str, source_files: Sequence[str]
) -> Dict[str, List[Tuple[int, int]]]:
    """Collect changed line ranges from git diff for the requested source files."""
    if not source_files:
        return {}

    relative_files = [
        str(Path(file_path).resolve().relative_to(root_path)).replace("\\", "/")
        for file_path in source_files
    ]
    cmd = [
        "git",
        "-C",
        str(root_path),
        "diff",
        "--unified=0",
        "--no-ext-diff",
        diff_base,
        "--",
        *relative_files,
    ]

    try:
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
    except subprocess.CalledProcessError as exc:
        stderr = (exc.stderr or "").strip()
        raise RuntimeError(
            stderr or "git diff failed while collecting changed line ranges"
        ) from exc

    changed_ranges: Dict[str, List[Tuple[int, int]]] = {}
    current_file: str | None = None

    for line in result.stdout.splitlines():
        if line.startswith("+++ "):
            current_file = resolve_diff_file_path(root_path, line)
            continue

        if not current_file:
            continue

        hunk_match = HUNK_HEADER_RE.match(line)
        if not hunk_match:
            continue

        start_line = int(hunk_match.group(1))
        line_count = int(hunk_match.group(2) or "1")
        if line_count <= 0:
            continue

        changed_ranges.setdefault(current_file, []).append(
            (start_line, start_line + line_count - 1)
        )

    return changed_ranges


def resolve_diff_file_path(root_path: Path, diff_line: str) -> str | None:
    """Resolve a git diff +++ line into an absolute file path."""
    if diff_line == "+++ /dev/null":
        return None

    relative_path = diff_line.removeprefix("+++ b/").removeprefix("+++ ")
    return str((root_path / relative_path).resolve())


def method_overlaps_changed_lines(
    method: Dict, changed_line_ranges: Dict[str, List[Tuple[int, int]]]
) -> bool:
    """Return true when a method intersects any changed hunk in its file."""
    method_ranges = changed_line_ranges.get(method["file"], [])
    return any(
        max(method["start_line"], range_start) <= min(method["end_line"], range_end)
        for range_start, range_end in method_ranges
    )


def method_starts_in_changed_lines(
    method: Dict, changed_line_ranges: Dict[str, List[Tuple[int, int]]]
) -> bool:
    """Return true when a method declaration line falls within a changed hunk."""
    return any(
        range_start <= method["start_line"] <= range_end
        for range_start, range_end in changed_line_ranges.get(method["file"], [])
    )


def calculate_crap_score(cyclomatic_complexity: int, coverage: float = 0.0) -> float:
    normalized_coverage = normalize_coverage(coverage)
    return (cyclomatic_complexity**2) * (
        (1 - normalized_coverage) ** 3
    ) + cyclomatic_complexity


def collect_lizard_methods(source_files: Sequence[str], coverage: float) -> List[Dict]:
    """Run lizard over a set of files and return all parsed methods."""
    if not source_files:
        return []

    cmd = [
        "lizard",
        "--language",
        "csharp",  # supports .cs and .razor.cs
        "--csv",
        *source_files,
    ]

    try:
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
    except FileNotFoundError:
        print("ERROR: lizard not found. Run: pip install lizard")
        exit(1)
    except subprocess.CalledProcessError as exc:
        print(f"ERROR: lizard failed: {exc.stderr}")
        exit(1)

    methods = []
    csv_reader = csv.reader(io.StringIO(result.stdout))

    for row in csv_reader:
        try:
            nloc = int(row[0])
            cc = int(row[1])
            filename = row[6]
            function = row[7]
            start_line = int(row[9])
            end_line = int(row[10]) if len(row) > 10 else start_line + nloc - 1

            if function == "*global*":
                continue

            resolved_file = str(Path(filename).resolve())
            if is_noise_file(Path(resolved_file)):
                continue

            methods.append(
                {
                    "file": resolved_file,
                    "function": function,
                    "cc": cc,
                    "nloc": nloc,
                    "crap_score": calculate_crap_score(cc, coverage),
                    "start_line": start_line,
                    "end_line": end_line,
                }
            )
        except (IndexError, ValueError):
            continue

    return methods


def collect_base_method_names(
    root_path: Path, diff_base: str, source_files: Sequence[str]
) -> Dict[str, set[str]]:
    """Collect method names that already exist in the diff base for each file."""
    base_method_names: Dict[str, set[str]] = {}

    with tempfile.TemporaryDirectory() as temp_dir:
        temp_files: List[str] = []
        temp_file_map: Dict[str, str] = {}

        for index, file_path in enumerate(source_files):
            resolved_file = str(Path(file_path).resolve())
            relative_path = str(Path(resolved_file).relative_to(root_path)).replace(
                "\\", "/"
            )
            result = subprocess.run(
                ["git", "-C", str(root_path), "show", f"{diff_base}:{relative_path}"],
                capture_output=True,
                text=True,
                check=False,
            )

            if result.returncode != 0:
                continue

            temp_file = Path(temp_dir) / f"{index}{Path(resolved_file).suffix}"
            temp_file.write_text(result.stdout, encoding="utf-8")
            resolved_temp_file = str(temp_file.resolve())
            temp_files.append(resolved_temp_file)
            temp_file_map[resolved_temp_file] = resolved_file

        for method in collect_lizard_methods(temp_files, 0.0):
            original_file = temp_file_map.get(method["file"])
            if original_file is None:
                continue

            base_method_names.setdefault(original_file, set()).add(method["function"])

    return base_method_names


def run_lizard(
    root_path: str,
    threshold: float = 15.0,
    coverage: float = 0.0,
    file_paths: Sequence[str] | None = None,
    changed_only: bool = False,
    new_methods_only: bool = False,
    diff_base: str = "HEAD",
) -> List[Dict]:
    """Run lizard on active C# files and return CRAP > threshold methods."""
    root = Path(root_path).resolve()
    source_files = resolve_source_files(root, file_paths)

    if not source_files:
        return []

    changed_line_ranges = (
        collect_changed_line_ranges(root, diff_base, source_files)
        if (changed_only or new_methods_only)
        else {}
    )

    base_method_names = (
        collect_base_method_names(root, diff_base, source_files)
        if new_methods_only
        else {}
    )

    methods = []
    for method in collect_lizard_methods(source_files, coverage):
        if changed_only and not method_overlaps_changed_lines(
            method, changed_line_ranges
        ):
            continue

        if new_methods_only and (
            not method_starts_in_changed_lines(method, changed_line_ranges)
            or method["function"] in base_method_names.get(method["file"], set())
        ):
            continue

        if method["crap_score"] > threshold:
            methods.append(method)

    return methods


def format_scope_label(
    file_paths: Sequence[str] | None, changed_only: bool, new_methods_only: bool
) -> str:
    """Describe the scanned scope in the report header."""
    scope = "active runtime code only (Components, Services, WileyCoWeb.Api, State, and root-level C# files)"
    if file_paths:
        scope = f"requested files only ({len(file_paths)} file(s))"

    if changed_only:
        scope = f"changed methods only within {scope}"

    if new_methods_only:
        scope = f"newly added methods only within {scope}"

    return scope


def print_report(
    methods: List[Dict],
    threshold: float,
    coverage: float,
    top_n: int,
    file_paths: Sequence[str] | None,
    changed_only: bool,
    new_methods_only: bool,
):
    """Pretty-print CRAP report."""
    if not methods:
        print(
            f"No methods with CRAP > {threshold:g} found in {format_scope_label(file_paths, changed_only, new_methods_only)}!"
        )
        return

    methods_to_print = sorted(
        methods, key=lambda x: (x["crap_score"], x["cc"], x["nloc"]), reverse=True
    )
    if top_n > 0:
        methods_to_print = methods_to_print[:top_n]

    print(
        f"\nWiley Widget Maintainability Report - {len(methods)} methods with CRAP > {threshold:g}\n"
    )
    print(f"Scope: {format_scope_label(file_paths, changed_only, new_methods_only)}")
    print(f"Coverage assumption: {normalize_coverage(coverage) * 100:.1f}%")
    if top_n > 0 and len(methods_to_print) < len(methods):
        print(
            f"Showing top {len(methods_to_print)} of {len(methods)} methods above threshold."
        )
    print()
    print(f"{'File':<60} {'Function':<50} {'CC':>4} {'NLOC':>6} {'CRAP':>8}")
    print("-" * 130)

    for m in methods_to_print:
        print(
            f"{m['file']:<60} {m['function']:<50} {m['cc']:>4} {m['nloc']:>6} {m['crap_score']:>8.1f}"
        )

    print(f"\nTotal high-CRAP items: {len(methods)}")
    print("   Run with --threshold 20 --top 50 for the worst offenders only.")


def main():
    parser = argparse.ArgumentParser(description="Find CRAP > N in WileyCoWeb")
    parser.add_argument("--path", default=".", help="Repo root (default: current dir)")
    parser.add_argument(
        "--threshold", type=float, default=15.0, help="CRAP threshold (default: 15)"
    )
    parser.add_argument(
        "--output", choices=["table", "csv"], default="table", help="Output format"
    )
    parser.add_argument(
        "--coverage",
        type=float,
        default=0.0,
        help="Approximate coverage as a fraction (0-1) or percent (0-100); default: 0",
    )
    parser.add_argument(
        "--top",
        type=int,
        default=0,
        help="Limit table output to the top N results; default: 0 shows all",
    )
    parser.add_argument(
        "--files",
        nargs="*",
        help="Optional list of specific C# files to scan (absolute or relative to --path)",
    )
    parser.add_argument(
        "--changed-only",
        action="store_true",
        help="Only report methods whose line ranges overlap changed git diff hunks",
    )
    parser.add_argument(
        "--new-methods-only",
        action="store_true",
        help="Only report added method declarations relative to the diff base",
    )
    parser.add_argument(
        "--diff-base",
        default="HEAD",
        help="Git revision to diff against when using --changed-only (default: HEAD)",
    )
    parser.add_argument(
        "--fail-on-results",
        action="store_true",
        help="Exit with code 1 when any methods exceed the threshold",
    )
    args = parser.parse_args()

    print("Scanning active WileyCoWeb runtime code for CRAP >", args.threshold, "...")
    high_crap_methods = run_lizard(
        args.path,
        args.threshold,
        args.coverage,
        args.files,
        args.changed_only,
        args.new_methods_only,
        args.diff_base,
    )

    if args.output == "csv":
        print("file,function,cc,nloc,crap_score")
        for m in high_crap_methods:
            print(
                f"{m['file']},{m['function']},{m['cc']},{m['nloc']},{m['crap_score']}"
            )
    else:
        print_report(
            high_crap_methods,
            args.threshold,
            args.coverage,
            args.top,
            args.files,
            args.changed_only,
            args.new_methods_only,
        )

    if args.fail_on_results and high_crap_methods:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
