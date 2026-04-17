#!/usr/bin/env python3
"""
AI Fetchable Manifest Generator v2.1 — Optimised for AI agents (Claude, Grok, Cursor)

Generates:
    - manifest.json               — primary manifest output for local AI review
    - ai-fetchable-manifest.json  — legacy compatible manifest output
    - AI-BRIEF.md                 — one-page architecture summary for AI agents

New in v2.1:
  - Configurable AI-BRIEF.md generation via architecture_hints
  - C# inheritance parsing for base class detection
  - .sln dependency parsing
  - Dynamic Syncfusion package detection from .csproj
  - XAML control usage analysis
  - Dependency graph building (ViewModel → Service relationships)
  - --compact CLI flag for faster generation
  - Enhanced architecture analysis with DI registration detection

New in v2.0:
  - Tiered content embedding: full text for critical/small files, smart preview otherwise
  - Priority scoring: ViewModels/Panels/MainForm/Services ranked by importance
  - critical_files + recommended_reading_order at manifest top level
  - NuGet dependency extraction from all .csproj files
  - AI-BRIEF.md auto-generation (architecture summary, ViewModel/Panel/Service lists)
  - manifest_mode: "compact" (metadata only) vs "full-context" (with embedded content)
  - Architecture analysis: auto-detect ViewModels, Panels, Services, Controls, Factories
  - Improved folder tree: depth + per-dir limits, forward-slash normalisation
  - Backward-compatible with existing .ai-manifest-config.json
  - Zero external dependencies — uses only stdlib + local git binary

Usage:
    python scripts/generate-ai-manifest.py [--compact] [--threads N]

Options:
    --compact    Skip content embedding for faster generation
    --threads N  Use N threads for parallel processing (default: 4, use 0 to disable)

Requirements:
    - Python 3.10+
    - git (must be on PATH)
"""

import hashlib
import json
import os
import re
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timedelta
from pathlib import Path, PurePosixPath
from threading import Lock
from typing import Any, Dict, List, Optional, Tuple

# ---------------------------------------------------------------------------
# Progress Bar Implementation (stdlib only)
# ---------------------------------------------------------------------------


class ProgressBar:
    """Simple progress bar using stdlib only."""

    def __init__(self, total: int, desc: str = "", width: int = 50):
        self.total = total
        self.current = 0
        self.desc = desc
        self.width = width
        self.start_time = time.time()
        self.last_update = 0
        self.update_interval = 1.0  # Update every 1 second
        self.last_percent = -1
        self.lock = Lock()  # Thread-safe progress updates

    def update(self, n: int = 1) -> None:
        """Update progress by n steps."""
        with self.lock:
            self.current += n
            now = time.time()

            # Only update display when percentage changes by at least 1%
            current_percent = int((self.current / self.total) * 100)
            if current_percent > self.last_percent:
                self.last_update = now
                self.last_percent = current_percent
                self._display()

    def set_description(self, desc: str) -> None:
        """Update the description."""
        with self.lock:
            self.desc = desc
            self._display()

    def _display(self) -> None:
        """Display the progress bar."""
        if self.total == 0:
            return

        percent = min(100, (self.current / self.total) * 100)
        filled = int(self.width * percent / 100)
        bar = "█" * filled + "░" * (self.width - filled)

        elapsed = time.time() - self.start_time
        if self.current > 0:
            eta = elapsed * (self.total - self.current) / self.current
            eta_str = f" ETA: {self._format_time(eta)}"
        else:
            eta_str = ""

        elapsed_str = f" {self._format_time(elapsed)}"

        print(
            f"\r{self.desc}: [{bar}] {percent:.1f}% ({self.current}/{self.total}){elapsed_str}{eta_str}",
            end="",
            flush=True,
        )

        if self.current >= self.total:
            print()  # New line when complete

    def _format_time(self, seconds: float) -> str:
        """Format seconds as MM:SS or HH:MM:SS."""
        seconds = int(seconds)
        hours, remainder = divmod(seconds, 3600)
        minutes, seconds = divmod(remainder, 60)

        if hours > 0:
            return f"{hours:02d}:{minutes:02d}:{seconds:02d}"
        else:
            return f"{minutes:02d}:{seconds:02d}"

    def close(self) -> None:
        """Finalize the progress bar."""
        with self.lock:
            if self.current < self.total:
                self.current = self.total
                self._display()
            print()


# ---------------------------------------------------------------------------
# Module-level constants
# ---------------------------------------------------------------------------

LANGUAGE_MAP: Dict[str, str] = {
    ".cs": "C#",
    ".xaml": "XAML",
    ".razor": "Razor",
    ".razorjs": "Razor JS",
    ".csproj": "C# Project",
    ".sln": "Visual Studio Solution",
    ".py": "Python",
    ".js": "JavaScript",
    ".ts": "TypeScript",
    ".tsx": "TypeScript JSX",
    ".json": "JSON",
    ".xml": "XML",
    ".md": "Markdown",
    ".txt": "Text",
    ".ps1": "PowerShell",
    ".config": "Configuration",
    ".html": "HTML",
    ".css": "CSS",
    ".scss": "SCSS",
    ".props": "MSBuild Props",
    ".targets": "MSBuild Targets",
}

DEFAULT_CRITICAL_GLOBS: List[str] = [
    "**/*ViewModel.cs",
    "**/*Panel.cs",
    "**/*Control.cs",
    "**/MainForm*.cs",
    "**/Analytics*.cs",
    "**/*Service.cs",
    "**/*Repository.cs",
    "**/Program.cs",
    "**/*.csproj",
    "**/App.xaml.cs",
]

DEFAULT_NEVER_EMBED: List[str] = [
    "**/bin/**",
    "**/obj/**",
    "**/*.Designer.cs",
    "**/*.g.cs",
    "**/*.g.i.cs",
    "**/*.AssemblyInfo.cs",
    "**/node_modules/**",
]

DEFAULT_ALWAYS_INCLUDE_DIRS: List[str] = [
    "src/WileyWidget.WinForms/Controls",
    "src/WileyWidget.WinForms/Forms",
    "src/WileyWidget.WinForms/ViewModels",
    "src/WileyWidget.Services",
]

DEFAULT_PRIORITY_PATTERNS: Dict[str, int] = {
    "MainForm": 100,
    "Program": 90,
    "ViewModel": 95,
    "Panel": 92,
    "Control": 88,
    "Service": 85,
    "Repository": 82,
    "Factory": 78,
    "Extension": 75,
    "Helper": 70,
    "Test": 65,
}

DEFAULT_EXCLUDED_PATH_PARTS = {
    ".git",
    ".vs",
    "bin",
    "obj",
    "node_modules",
    "WebView2Runtime",
}


class AIManifestGenerator:
    """Generates an AI-optimised fetchable manifest for repository visibility."""

    def __init__(self, repo_root: Path, max_threads: int = 4) -> None:
        self.repo_root = repo_root.resolve()
        self.max_threads = max_threads
        self.config = self._load_config()
        self.exclude_patterns = self._compile_patterns()
        self.focus_extensions: set[str] = set(
            self.config.get("include_only_extensions", [])
        )
        self.max_file_size_bytes: int = int(
            self.config.get("max_file_size_bytes", 10 * 1024 * 1024)
        )
        self.max_files: int = int(self.config.get("max_files", 12000))
        self.manifest_mode: str = self.config.get("manifest_mode", "full-context")
        self.generate_context_summary: bool = bool(
            self.config.get("generate_context_summary", True)
        )

        # v2.0 — content inclusion settings (nested under "content_inclusion" key)
        ci = self.config.get("content_inclusion", {})
        self.embed_full_if_smaller_than_kb: float = float(
            ci.get("embed_full_if_smaller_than_kb", 600)
        )
        self.preview_lines: int = int(ci.get("preview_lines_for_large_files", 800))
        self.critical_globs: List[str] = ci.get(
            "critical_globs", DEFAULT_CRITICAL_GLOBS
        )
        self.never_embed_globs: List[str] = ci.get("never_embed", DEFAULT_NEVER_EMBED)
        self.max_embedded_files: int = int(ci.get("max_embedded_files", 400))

        # Directory / focus settings
        self.always_include_dirs: List[str] = self.config.get(
            "always_include_dirs", DEFAULT_ALWAYS_INCLUDE_DIRS
        )
        self.focus_directories: List[str] = self.config.get(
            "focus_directories", ["src"]
        )

        # Priority scoring map
        self.priority_patterns: Dict[str, int] = self.config.get(
            "priority_patterns", DEFAULT_PRIORITY_PATTERNS
        )

        # New config sections for v2.1
        self.architecture_hints = self.config.get("architecture_hints", {})
        self.project_analysis = self.config.get("project_analysis", {})

        self.repo_info = self._get_repo_info()

        # Runtime accumulators — populated during _scan_files()
        self._total_files = 0
        self._total_size = 0
        self._categories: Dict[str, int] = {}
        self._languages: Dict[str, int] = {}
        self._files_truncated = False
        self._embedded_count = 0
        self._files: List[Dict[str, Any]] = []

    # ------------------------------------------------------------------
    # Config & repo bootstrap
    # ------------------------------------------------------------------

    def _load_config(self) -> Dict[str, Any]:
        """Load configuration from .ai-manifest-config.json."""
        candidate_paths = [
            self.repo_root / ".ai-manifest-config.json",
            self.repo_root / "Scripts" / ".ai-manifest-config.json",
            self.repo_root / "Scripts" / ".ai-manifest-config.json.example",
        ]

        config_path = next((path for path in candidate_paths if path.exists()), None)
        if config_path is None:
            raise FileNotFoundError(
                "Config file not found in repo root or Scripts/ directory"
            )
        with open(config_path, "r", encoding="utf-8") as f:
            return json.load(f)

    def _compile_patterns(self) -> List[re.Pattern[str]]:
        """Compile regex patterns for file exclusion."""
        patterns: List[re.Pattern[str]] = []
        for pat in self.config.get("exclude_patterns", []):
            try:
                patterns.append(re.compile(pat, re.IGNORECASE))
            except re.error as exc:
                print(f"Warning: Invalid regex pattern '{pat}': {exc}", file=sys.stderr)
        return patterns

    def _is_builtin_excluded_path(self, rel_path: str) -> bool:
        """Exclude common generated directories even when config regexes miss them."""
        parts = {part.lower() for part in PurePosixPath(rel_path).parts}
        excluded = {part.lower() for part in DEFAULT_EXCLUDED_PATH_PARTS}
        return bool(parts & excluded)

    def _git(self, *args: str) -> str:
        """Run a git command in repo_root and return stdout (stripped)."""
        result = subprocess.run(
            ["git", *args],
            cwd=self.repo_root,
            capture_output=True,
            text=True,
        )
        return result.stdout.strip()

    def _get_repo_info(self) -> Dict[str, Any]:
        """Get repository information using git."""
        if hasattr(self, "_repo_info_cache"):
            return self._repo_info_cache  # type: ignore[attr-defined]

        try:
            remote_url = self._git("remote", "get-url", "origin") or "unknown"
            branch = self._git("rev-parse", "--abbrev-ref", "HEAD") or "(detached)"
            commit_hash = self._git("rev-parse", "HEAD") or "unknown"
            is_dirty = bool(self._git("status", "--porcelain"))
            owner_repo = self._extract_owner_repo(remote_url)
            now = datetime.now()
            self._repo_info_cache = {
                "remote_url": remote_url,
                "owner_repo": owner_repo,
                "branch": branch,
                "commit_hash": commit_hash,
                "is_dirty": is_dirty,
                "generated_at": now.isoformat(),
                "valid_until": (now + timedelta(days=7)).isoformat(),
            }
            return self._repo_info_cache
        except Exception as exc:
            raise RuntimeError(f"Failed to get git info: {exc}") from exc

    def _extract_owner_repo(self, remote_url: str) -> str:
        """Extract owner/repo from git remote URL."""
        cleaned = remote_url.strip().removesuffix(".git")
        if cleaned.startswith("git@"):
            cleaned = cleaned.replace(":", "/", 1).partition("@")[2]
        cleaned = re.sub(r"^[a-zA-Z]+://", "", cleaned)
        parts = cleaned.split("/")
        if len(parts) >= 2 and parts[-2] and parts[-1]:
            return f"{parts[-2]}/{parts[-1]}"
        return "unknown/unknown"

    # ------------------------------------------------------------------
    # Inclusion / exclusion
    # ------------------------------------------------------------------

    def _norm(self, path: Path) -> str:
        """Return forward-slash repo-relative path string."""
        return str(path.relative_to(self.repo_root)).replace("\\", "/")

    def _matches_any_glob(self, rel_str: str, globs: List[str]) -> bool:
        """Test a repo-relative path string against a list of glob patterns."""
        p = Path(rel_str)
        return any(p.match(g) for g in globs)

    def _should_include_path(self, path: Path, is_dir: bool = False) -> bool:
        """Broad path filter used for the folder-tree walk."""
        rel = self._norm(path)
        if self._is_builtin_excluded_path(rel):
            return False
        for pattern in self.exclude_patterns:
            if pattern.search(rel):
                return False
        if is_dir:
            return True
        if self.config.get("focus_mode", False) and self.focus_extensions:
            if path.suffix.lower() not in self.focus_extensions:
                return False
        return True

    def _should_include_file(self, path: Path) -> bool:
        """Determine whether a file should appear in the manifest files list."""
        rel = self._norm(path)

        if self._is_builtin_excluded_path(rel):
            return False

        # Always include files in always_include_dirs (even in focus mode)
        if any(rel.startswith(d.replace("\\", "/")) for d in self.always_include_dirs):
            # Still respect hard exclusions (bin, obj, etc.)
            return not any(p.search(rel) for p in self.exclude_patterns)

        # src/ files by pass focus-extension filtering
        if rel.startswith("src/"):
            return not any(p.search(rel) for p in self.exclude_patterns)

        # All other files: check exclude patterns first
        for pattern in self.exclude_patterns:
            if pattern.search(rel):
                return False

        # Focus mode — restrict by extension
        if self.config.get("focus_mode", False) and self.focus_extensions:
            if path.suffix.lower() not in self.focus_extensions:
                return False

        return True

    def _is_critical(self, rel_str: str) -> bool:
        return self._matches_any_glob(rel_str, self.critical_globs)

    def _never_embed(self, rel_str: str) -> bool:
        return self._matches_any_glob(rel_str, self.never_embed_globs)

    # ------------------------------------------------------------------
    # Priority scoring
    # ------------------------------------------------------------------

    def _calculate_priority(self, rel_str: str) -> int:
        """Score a file 0-100 based on its name matching priority_patterns."""
        stem = Path(rel_str).stem
        score = 50
        for keyword, pts in self.priority_patterns.items():
            if keyword.lower() in stem.lower():
                score = max(score, pts)
        # Boost files inside always_include_dirs
        if any(
            rel_str.startswith(d.replace("\\", "/")) for d in self.always_include_dirs
        ):
            score = min(score + 10, 100)
        return score

    # ------------------------------------------------------------------
    # Content embedding (v2.0)
    # ------------------------------------------------------------------

    def _get_content(
        self, file_path: Path, rel_str: str, size_kb: float
    ) -> Optional[Dict[str, Any]]:
        """
        Return a content block for embedding in the manifest.

        Rules (applied in order):
          1. compact mode             → None (no embedding)
          2. never_embed glob match   → None
          3. is_critical OR size_kb <= embed_full_if_smaller_than_kb → full embed
          4. otherwise                → smart preview (first N + last 200 lines)
        """
        if self.manifest_mode == "compact":
            return None
        if self._never_embed(rel_str):
            return None

        try:
            with open(file_path, "r", encoding="utf-8", errors="ignore") as fh:
                content = fh.read()

            if (
                self._is_critical(rel_str)
                or size_kb <= self.embed_full_if_smaller_than_kb
            ):
                return {"content": content, "mode": "full"}

            # Smart preview for large non-critical files
            lines = content.splitlines(keepends=True)
            n = len(lines)
            if n <= self.preview_lines + 200:
                return {"content": content, "mode": "full"}

            head = "".join(lines[: self.preview_lines])
            tail = "".join(lines[-200:])
            skipped = n - self.preview_lines - 200
            preview = head + f"\n\n... [{skipped} lines truncated] ...\n\n" + tail
            return {"content": preview, "mode": "preview", "total_lines": n}

        except Exception as exc:
            return {"content": "", "mode": "error", "error": str(exc)}

    # ------------------------------------------------------------------
    # NuGet extraction
    # ------------------------------------------------------------------

    def _parse_nuget_deps(self) -> List[Dict[str, str]]:
        """Extract PackageReference entries from all .csproj files."""
        seen: set[Tuple[str, str]] = set()
        deps: List[Dict[str, str]] = []
        for csproj in sorted(self.repo_root.rglob("*.csproj")):
            try:
                with open(csproj, "r", encoding="utf-8") as fh:
                    text = fh.read()
                for m in re.finditer(
                    r'<PackageReference\s+Include="([^"]+)"\s+Version="([^"]+)"', text
                ):
                    key = (m.group(1), m.group(2))
                    if key not in seen:
                        seen.add(key)
                        deps.append(
                            {
                                "package": m.group(1),
                                "version": m.group(2),
                                "source": self._norm(csproj),
                            }
                        )
            except Exception:
                pass
        return sorted(deps, key=lambda d: d["package"].lower())

    # ------------------------------------------------------------------
    # Git helpers
    # ------------------------------------------------------------------

    def _is_tracked(self, relative_path: Path) -> bool:
        """Determine if a file is tracked by git (best-effort)."""
        try:
            return bool(self._git("ls-files", str(relative_path)))
        except Exception:
            return False

    # ------------------------------------------------------------------
    # Hashing & language detection
    # ------------------------------------------------------------------

    def _calculate_sha256(self, file_path: Path) -> str:
        """Calculate SHA256 hash of a file."""
        h = hashlib.sha256()
        with open(file_path, "rb") as fh:
            for chunk in iter(lambda: fh.read(65536), b""):
                h.update(chunk)
        return h.hexdigest()

    def _detect_language(self, file_path: Path) -> str:
        """Detect programming language from file extension."""
        return LANGUAGE_MAP.get(file_path.suffix.lower(), "Unknown")

    def _should_include_file_compat(self, path: Path) -> bool:
        """Alias kept for any callers that use the old name."""
        return self._should_include_file(path)

    def _read_text_file(self, file_path: Path) -> str:
        """Read a text file for analysis helpers without mutating manifest entries."""
        try:
            return file_path.read_text(encoding="utf-8")
        except Exception:
            return ""

    def _attach_content_blocks(self, files: List[Dict[str, Any]]) -> int:
        """Attach content blocks to the highest-priority files within the configured cap."""
        if self.manifest_mode == "compact" or self.max_embedded_files <= 0:
            return 0

        embedded_count = 0
        for file_info in files:
            if embedded_count >= self.max_embedded_files:
                break

            rel_str = file_info["metadata"]["path"]
            file_path = self.repo_root / Path(rel_str)
            size_kb = float(file_info["metadata"]["size_kb"])
            content_block = self._get_content(file_path, rel_str, size_kb)
            if content_block is None:
                continue

            file_info["content_info"] = content_block
            embedded_count += 1

        return embedded_count

    # ------------------------------------------------------------------
    # File scanning
    # ------------------------------------------------------------------

    def _scan_files(self) -> List[Dict[str, Any]]:
        """Scan repository files, collect metadata, and optionally embed content."""
        files: List[Dict[str, Any]] = []
        total_size = 0
        categories: Dict[str, int] = {}
        languages: Dict[str, int] = {}
        embedded_count = 0

        repo_info = self.repo_info

        # First pass: count total files for progress bar
        print("🔍 Scanning repository files...")
        total_files_estimate = 0
        for dirpath, dirnames, filenames in os.walk(self.repo_root, topdown=True):
            current_dir = Path(dirpath)
            dirnames[:] = [
                d
                for d in sorted(dirnames)
                if self._should_include_path(current_dir / d, is_dir=True)
            ]

            for filename in sorted(filenames):
                file_path = current_dir / filename
                if not file_path.is_file():
                    continue

                rel_path = self._norm(file_path)
                if self._is_builtin_excluded_path(rel_path) or rel_path.endswith(".secret"):
                    continue

                if self._should_include_file(file_path):
                    total_files_estimate += 1

        progress = ProgressBar(total_files_estimate, "📁 Processing files")

        # Collect all file paths to process
        file_paths_to_process = []
        for dirpath, dirnames, filenames in os.walk(self.repo_root, topdown=True):
            current_dir = Path(dirpath)
            dirnames[:] = [
                d
                for d in sorted(dirnames)
                if self._should_include_path(current_dir / d, is_dir=True)
            ]

            for filename in sorted(filenames):
                file_path = current_dir / filename
                if not file_path.is_file():
                    continue

                rel_path = self._norm(file_path)
                if self._is_builtin_excluded_path(rel_path) or rel_path.endswith(".secret"):
                    continue

                if self._should_include_file(file_path):
                    file_paths_to_process.append(file_path)

                    if self.max_files and len(file_paths_to_process) >= self.max_files:
                        self._files_truncated = True
                        break

            if self.max_files and len(file_paths_to_process) >= self.max_files:
                break

        # Process files with multithreading if enabled
        if self.max_threads > 0:
            print(
                f"🔄 Using {self.max_threads} threads for parallel file processing..."
            )
            start_time = time.time()

            def process_file(file_path: Path) -> Optional[Dict[str, Any]]:
                """Process a single file and return its info dict."""
                try:
                    rel_path = self._norm(file_path)
                    stat = file_path.stat()
                    size = stat.st_size

                    if self.max_file_size_bytes and size > self.max_file_size_bytes:
                        return None

                    size_kb = round(size / 1024, 1)
                    last_modified = datetime.fromtimestamp(stat.st_mtime).isoformat()
                    relative_path = file_path.relative_to(self.repo_root)
                    rel_str = rel_path
                    sha256 = self._calculate_sha256(file_path)
                    language = self._detect_language(file_path)
                    priority = self._calculate_priority(rel_str)
                    tracked = self._is_tracked(relative_path)

                    ext = file_path.suffix.lower()
                    if ext in {
                        ".cs",
                        ".xaml",
                        ".razor",
                        ".py",
                        ".js",
                        ".ts",
                        ".tsx",
                        ".ps1",
                    }:
                        category = "source_code"
                    elif "test" in rel_str.lower():
                        category = "test"
                    elif ext in {
                        ".csproj",
                        ".sln",
                        ".json",
                        ".xml",
                        ".props",
                        ".targets",
                    }:
                        category = "config"
                    else:
                        category = "other"

                    file_info: Dict[str, Any] = {
                        "metadata": {
                            "path": rel_str,
                            "exists": True,
                            "size_bytes": size,
                            "size_kb": size_kb,
                            "last_modified": last_modified,
                            "language": language,
                            "priority": priority,
                            "is_critical": self._is_critical(rel_str),
                        },
                        "urls": {
                            "blob_url": (
                                f"https://github.com/{repo_info['owner_repo']}/blob/"
                                f"{repo_info['branch']}/{rel_str}"
                            ),
                            "raw_url": (
                                f"https://raw.githubusercontent.com/{repo_info['owner_repo']}/"
                                f"{repo_info['branch']}/{rel_str}"
                            ),
                        },
                        "context": {
                            "category": category,
                            "tracked": tracked,
                            "extension": file_path.suffix,
                            "sha256": sha256,
                        },
                    }

                    return file_info

                except (OSError, IOError) as exc:
                    print(
                        f"Warning: Could not process {file_path}: {exc}",
                        file=sys.stderr,
                    )
                    return None

            # Use ThreadPoolExecutor for parallel processing
            with ThreadPoolExecutor(max_workers=self.max_threads) as executor:
                future_to_file = {
                    executor.submit(process_file, fp): fp
                    for fp in file_paths_to_process
                }

                for future in as_completed(future_to_file):
                    file_info = future.result()
                    if file_info is not None:
                        files.append(file_info)

                        # Update shared counters (thread-safe since we're in main thread)
                        size = file_info["metadata"]["size_bytes"]
                        total_size += size

                        category = file_info["context"]["category"]
                        categories[category] = categories.get(category, 0) + 1

                        language = file_info["metadata"]["language"]
                        languages[language] = languages.get(language, 0) + 1

                    progress.update()

            elapsed = time.time() - start_time
            print(f"✅ Parallel processing completed in {elapsed:.1f}s")
            progress.close()
        else:
            # Single-threaded processing (original logic)
            processed_count = 0
            for file_path in file_paths_to_process:
                try:
                    rel_path = self._norm(file_path)
                    stat = file_path.stat()
                    size = stat.st_size
                    if self.max_file_size_bytes and size > self.max_file_size_bytes:
                        self._files_truncated = True
                        continue

                    size_kb = round(size / 1024, 1)
                    last_modified = datetime.fromtimestamp(stat.st_mtime).isoformat()
                    relative_path = file_path.relative_to(self.repo_root)
                    rel_str = rel_path
                    sha256 = self._calculate_sha256(file_path)
                    language = self._detect_language(file_path)
                    priority = self._calculate_priority(rel_str)
                    tracked = self._is_tracked(relative_path)

                    ext = file_path.suffix.lower()
                    if ext in {
                        ".cs",
                        ".xaml",
                        ".razor",
                        ".py",
                        ".js",
                        ".ts",
                        ".tsx",
                        ".ps1",
                    }:
                        category = "source_code"
                    elif "test" in rel_str.lower():
                        category = "test"
                    elif ext in {
                        ".csproj",
                        ".sln",
                        ".json",
                        ".xml",
                        ".props",
                        ".targets",
                    }:
                        category = "config"
                    else:
                        category = "other"

                    categories[category] = categories.get(category, 0) + 1
                    languages[language] = languages.get(language, 0) + 1
                    total_size += size

                    file_info: Dict[str, Any] = {
                        "metadata": {
                            "path": rel_str,
                            "exists": True,
                            "size_bytes": size,
                            "size_kb": size_kb,
                            "last_modified": last_modified,
                            "language": language,
                            "priority": priority,
                            "is_critical": self._is_critical(rel_str),
                        },
                        "urls": {
                            "blob_url": (
                                f"https://github.com/{repo_info['owner_repo']}/blob/"
                                f"{repo_info['branch']}/{rel_str}"
                            ),
                            "raw_url": (
                                f"https://raw.githubusercontent.com/{repo_info['owner_repo']}/"
                                f"{repo_info['branch']}/{rel_str}"
                            ),
                        },
                        "context": {
                            "category": category,
                            "tracked": tracked,
                            "extension": file_path.suffix,
                            "sha256": sha256,
                        },
                    }

                    files.append(file_info)
                    processed_count += 1
                    progress.update()

                except (OSError, IOError) as exc:
                    print(
                        f"Warning: Could not process {file_path}: {exc}",
                        file=sys.stderr,
                    )
                    processed_count += 1
                    progress.update()

        progress.close()

        # Sort by priority descending so most important files surface first
        files.sort(key=lambda x: x["metadata"]["priority"], reverse=True)
        embedded_count = self._attach_content_blocks(files)

        self._total_files = len(files)
        self._total_size = total_size
        self._categories = categories
        self._languages = languages
        self._embedded_count = embedded_count

        return files

    # ------------------------------------------------------------------
    # Metrics
    # ------------------------------------------------------------------

    def _count_code_metrics(self) -> Dict[str, Any]:
        """Calculate LOC, complexity, and test coverage from source files."""
        total_lines = 0
        code_lines = 0
        comment_lines = 0
        blank_lines = 0
        complexity_sum = 0
        complexity_count = 0
        test_file_count = 0

        src_files = [
            Path(f["metadata"]["path"])
            for f in self._files
            if f["metadata"]["language"] in ("C#", "Python")
        ]

        for rel_path in src_files:
            file_path = self.repo_root / rel_path
            try:
                with open(file_path, "r", encoding="utf-8", errors="ignore") as fh:
                    lines = fh.readlines()

                for line in lines:
                    stripped = line.strip()
                    total_lines += 1
                    if not stripped:
                        blank_lines += 1
                    elif stripped.startswith(("#", "//")):
                        comment_lines += 1
                    else:
                        code_lines += 1

                # Cyclomatic-complexity estimate for Factory / Docking C# files
                if file_path.suffix == ".cs" and (
                    "Factory" in file_path.name or "Docking" in str(rel_path)
                ):
                    content = "".join(lines)
                    cx = (
                        content.count(" if ")
                        + content.count(" if(")
                        + content.count(" else ")
                        + content.count(" switch ")
                        + content.count(" case ")
                        + content.count(" catch ")
                        + content.count(" for ")
                        + content.count(" for(")
                        + content.count(" foreach ")
                        + content.count(" while ")
                        + content.count(" && ")
                        + content.count(" || ")
                        + content.count("?")
                        + 1
                    )
                    complexity_sum += cx
                    complexity_count += 1

                if "test" in str(rel_path).lower():
                    test_file_count += 1

            except Exception:
                pass

        avg_cx = complexity_sum / max(complexity_count, 1)
        return {
            "total_lines_of_code": total_lines,
            "total_code_lines": code_lines,
            "total_comment_lines": comment_lines,
            "total_blank_lines": blank_lines,
            "average_complexity": round(avg_cx, 2),
            "test_count": test_file_count,
        }

    # ------------------------------------------------------------------
    # Architecture analysis (v2.0)
    # ------------------------------------------------------------------

    def _analyze_architecture(self) -> Dict[str, Any]:
        """Auto-detect ViewModels, Panels, Services, Controls, etc. from file paths and C# content."""
        print("🔬 Analyzing codebase architecture...")
        analysis_progress = ProgressBar(5, "🔍 Architecture analysis")

        views: List[str] = []
        viewmodels: List[str] = []
        services: List[str] = []
        controls: List[str] = []
        panels: List[str] = []
        repositories: List[str] = []
        factories: List[str] = []
        base_classes: Dict[str, List[str]] = {}
        di_registrations: List[str] = []
        syncfusion_packages: List[str] = []

        # First pass: filename-based detection
        for f in self._files:
            path = f["metadata"]["path"]
            name = Path(path).name
            if "ViewModel" in name:
                viewmodels.append(path)
            elif "Panel" in name and name.endswith(".cs"):
                panels.append(path)
            elif "Control" in name and name.endswith(".cs"):
                controls.append(path)
            elif "Service" in name and name.endswith(".cs"):
                services.append(path)
            elif "Repository" in name and name.endswith(".cs"):
                repositories.append(path)
            elif "Factory" in name and name.endswith(".cs"):
                factories.append(path)
            elif (
                ("Form" in name or "View" in name)
                and name.endswith(".cs")
                and "ViewModel" not in name
            ):
                views.append(path)

        analysis_progress.update()
        analysis_progress.set_description("🔬 Parsing C# inheritance")

        # Second pass: C# content analysis if enabled
        if self.project_analysis.get("parse_csharp_inheritance", False):
            key_bases = self.architecture_hints.get("key_base_classes", [])
            for base in key_bases:
                base_classes[base] = []

            if self.max_threads > 0:
                # Multithreaded C# analysis
                def analyze_csharp_file(f):
                    """Analyze a single C# file for inheritance and DI patterns."""
                    if not f["metadata"]["path"].endswith(".cs"):
                        return None

                    file_path = self.repo_root / f["metadata"]["path"]
                    content = self._read_text_file(file_path)

                    if not content:
                        return None

                    results = {
                        "path": f["metadata"]["path"],
                        "bases": [],
                        "di_registrations": [],
                    }

                    # Parse class inheritance
                    for base in key_bases:
                        if f" : {base}" in content or f": {base}<" in content:
                            results["bases"].append(base)

                    # Parse DI registrations
                    if (
                        "AddTransient" in content
                        or "AddScoped" in content
                        or "AddSingleton" in content
                    ):
                        results["di_registrations"].append(f["metadata"]["path"])

                    return results

                # Process C# files in parallel
                cs_files = [
                    f for f in self._files if f["metadata"]["path"].endswith(".cs")
                ]
                with ThreadPoolExecutor(
                    max_workers=min(self.max_threads, len(cs_files))
                ) as executor:
                    futures = [
                        executor.submit(analyze_csharp_file, f) for f in cs_files
                    ]

                    for future in as_completed(futures):
                        result = future.result()
                        if result:
                            for base in result["bases"]:
                                base_classes[base].append(result["path"])
                            di_registrations.extend(result["di_registrations"])
            else:
                # Single-threaded analysis (original logic)
                for f in self._files:
                    if not f["metadata"]["path"].endswith(".cs"):
                        continue
                    # Read content directly for analysis
                    file_path = self.repo_root / f["metadata"]["path"]
                    content = self._read_text_file(file_path)

                    if not content:
                        continue

                    # Parse class inheritance
                    for base in key_bases:
                        if f" : {base}" in content or f": {base}<" in content:
                            base_classes[base].append(f["metadata"]["path"])

                    # Parse DI registrations
                    if (
                        "AddTransient" in content
                        or "AddScoped" in content
                        or "AddSingleton" in content
                    ):
                        di_registrations.append(f["metadata"]["path"])

        analysis_progress.update()
        analysis_progress.set_description("📦 Detecting Syncfusion packages")

        # Detect Syncfusion packages from csproj if enabled
        if self.project_analysis.get("detect_tech_from_csproj", False):
            syncfusion_packages = self._detect_syncfusion_packages()

        analysis_progress.update()
        analysis_progress.set_description("🔗 Analyzing project dependencies")

        # Parse .sln for project relationships if enabled
        project_deps = {}
        if self.project_analysis.get("parse_sln_dependencies", False):
            project_deps = self._parse_sln_dependencies()

        analysis_progress.update()
        analysis_progress.set_description("🎨 Analyzing XAML controls")

        # Analyze XAML controls if enabled
        xaml_controls = {}
        if self.project_analysis.get("analyze_xaml_controls", False):
            xaml_controls = self._analyze_xaml_controls()

        analysis_progress.update()
        analysis_progress.set_description("🔗 Building dependency graph")

        # Build dependency graph if enabled
        dep_graph = {}
        if self.project_analysis.get("build_dependency_graph", False):
            dep_graph = self._build_dependency_graph()

        analysis_progress.update()
        analysis_progress.close()

        return {
            "pattern": "MVVM",
            "views": views,
            "viewmodels": viewmodels,
            "panels": panels,
            "services": services,
            "controls": controls,
            "repositories": repositories,
            "factories": factories,
            "models": [],
            "converters": [],
            "behaviors": [],
            "modules": [],
            "base_classes": base_classes,
            "di_registrations": di_registrations,
            "syncfusion_packages": syncfusion_packages,
            "project_dependencies": project_deps,
            "xaml_controls": xaml_controls,
            "dependency_graph": dep_graph,
            "counts": {
                "views": len(views),
                "viewmodels": len(viewmodels),
                "panels": len(panels),
                "services": len(services),
                "controls": len(controls),
                "repositories": len(repositories),
                "factories": len(factories),
            },
        }

    def _detect_syncfusion_packages(self) -> List[str]:
        """Scan .csproj files for Syncfusion package references."""
        packages = set()
        for csproj in self.repo_root.rglob("*.csproj"):
            try:
                content = csproj.read_text(encoding="utf-8")
                # Look for PackageReference with Syncfusion
                matches = re.findall(
                    r'<PackageReference\s+Include="([^"]*Syncfusion[^"]*)"',
                    content,
                    re.IGNORECASE,
                )
                packages.update(matches)
            except Exception:
                continue
        return sorted(packages)

    def _parse_sln_dependencies(self) -> Dict[str, List[str]]:
        """Parse .sln file for project relationships."""
        deps = {}
        sln_files = list(self.repo_root.glob("*.sln"))
        if not sln_files:
            return deps

        try:
            sln_content = sln_files[0].read_text(encoding="utf-8")
            # Extract project references (simplified parsing)
            project_pattern = (
                r'Project\("\{[^}]+\}"\)\s*=\s*"([^"]+)",\s*"([^"]+)",\s*"\{[^}]+\}"'
            )
            projects = re.findall(project_pattern, sln_content, re.MULTILINE)

            for name, path in projects:
                if path.endswith(".csproj"):
                    deps[name] = [path]
        except Exception:
            pass
        return deps

    def _analyze_xaml_controls(self) -> Dict[str, int]:
        """Analyze XAML files for Syncfusion control usage."""
        controls = {}
        for xaml_file in self.repo_root.rglob("*.xaml"):
            try:
                content = xaml_file.read_text(encoding="utf-8")
                # Look for Syncfusion controls (xmlns:sf="..." or sf:ControlName)
                sf_controls = re.findall(r"<sf:([A-Za-z]+)", content)
                for control in sf_controls:
                    controls[control] = controls.get(control, 0) + 1
            except Exception:
                continue
        return dict(sorted(controls.items(), key=lambda x: x[1], reverse=True))

    def _build_dependency_graph(self) -> Dict[str, List[str]]:
        """Build a simple dependency graph from ViewModels to Services."""
        graph = {}
        for f in self._files:
            if not f["metadata"]["path"].endswith(".cs"):
                continue
            file_path = self.repo_root / f["metadata"]["path"]
            content = self._read_text_file(file_path)
            if not content:
                continue

            path = f["metadata"]["path"]
            deps = []

            # Look for service injections or usages
            service_matches = re.findall(r"(\w+Service)\s", content)
            for match in service_matches:
                if match != "Service":  # Avoid generic matches
                    deps.append(match)

            if deps:
                graph[path] = list(set(deps))
        return graph

    # ------------------------------------------------------------------
    # Critical files & reading order (v2.0)
    # ------------------------------------------------------------------

    def _build_critical_and_reading_order(
        self,
    ) -> Tuple[List[Dict[str, Any]], List[str]]:
        """Build critical_files list and recommended_reading_order from scored files."""
        threshold_critical = 82
        threshold_reading = 90

        critical: List[Dict[str, Any]] = []
        reading_order: List[str] = []

        for f in self._files:
            priority = f["metadata"]["priority"]
            path = f["metadata"]["path"]

            if priority >= threshold_critical:
                stem = Path(path).stem
                file_type = next(
                    (kw for kw in self.priority_patterns if kw.lower() in stem.lower()),
                    "Source",
                )
                critical.append(
                    {
                        "path": path,
                        "priority": priority,
                        "type": file_type,
                        "reason": f"{file_type} — priority {priority}",
                    }
                )

            if priority >= threshold_reading:
                reading_order.append(path)

        critical.sort(key=lambda x: x["priority"], reverse=True)
        return critical[:50], reading_order[:30]

    # ------------------------------------------------------------------
    # Summary
    # ------------------------------------------------------------------

    def _generate_summary(self) -> Dict[str, Any]:
        """Generate summary statistics."""
        return {
            "total_files": self._total_files,
            "files_in_manifest": self._total_files,
            "files_truncated": self._files_truncated,
            "total_size_bytes": self._total_size,
            "total_size_kb": round(self._total_size / 1024, 1),
            "categories": self._categories,
            "languages": self._languages,
            "manifest_mode": self.manifest_mode,
            "embedded_files": self._embedded_count,
        }

    # ------------------------------------------------------------------
    # Folder tree
    # ------------------------------------------------------------------

    def _generate_folder_tree(self) -> Dict[str, Any]:
        """Generate a folder tree structure with depth and per-dir limits."""
        max_depth: int = int(self.config.get("tree_max_depth", 5))
        max_per_dir: int = int(self.config.get("max_tree_entries_per_dir", 50))

        def build(path: Path, depth: int = 0) -> Dict[str, Any]:
            if path.is_file():
                return {
                    "name": path.name,
                    "type": "file",
                    "path": self._norm(path),
                }

            children: List[Dict[str, Any]] = []
            try:
                entries = sorted(path.iterdir())
                count = 0
                for child in entries:
                    if count >= max_per_dir:
                        children.append(
                            {
                                "name": f"... ({len(entries) - count} more)",
                                "type": "truncated",
                            }
                        )
                        break
                    if child.is_dir() and self._should_include_path(child, is_dir=True):
                        if depth < max_depth:
                            children.append(build(child, depth + 1))
                        else:
                            children.append(
                                {
                                    "name": child.name,
                                    "type": "directory",
                                    "path": self._norm(child),
                                    "children": ["[depth limit reached]"],
                                }
                            )
                        count += 1
                    elif child.is_file() and self._should_include_path(child):
                        children.append(build(child, depth + 1))
                        count += 1
            except PermissionError:
                pass

            return {
                "name": path.name,
                "type": "directory",
                "path": self._norm(path),
                "children": children,
            }

        return build(self.repo_root)

    # ------------------------------------------------------------------
    # AI-BRIEF.md generation (v2.0)
    # ------------------------------------------------------------------

    def _generate_ai_brief(self, manifest: Dict[str, Any]) -> Path:
        """Write AI-BRIEF.md — a one-page architecture summary for AI agents."""
        arch = manifest["architecture"]
        brief_path = self.repo_root / "AI-BRIEF.md"
        repo = manifest["repository"]
        summary = manifest["summary"]
        hints = self.architecture_hints

        lines: List[str] = [
            "# WileyCoWeb — AI Briefing",
            f"> Generated: {datetime.now():%Y-%m-%d %H:%M}  |  "
            f"Branch: `{repo.get('branch', '?')}`  |  "
            f"Commit: `{repo.get('commit_hash', '?')[:10]}`",
            "",
            "## Project Purpose",
            f"WileyCoWeb is a {hints.get('project_type', 'Blazor WebAssembly application')} built with {hints.get('ui_framework', 'Syncfusion Blazor component suite')} "
            f"and {hints.get('architecture_pattern', 'MVVM-inspired architecture with dependency injection')}.",
            "",
            "**Important Context**: Files in the `src/` folder are archived legacy code from a previous WinForms version and are **not participating** in the current Blazor application. The active codebase consists of `Components/`, `Services/`, `WileyCoWeb.Api/`, and root-level files.",
            "",
            "## Architecture Patterns",
            f"- **{hints.get('architecture_pattern', 'MVVM').split()[0]}** — {hints.get('architecture_pattern', 'Components with dependency injection')}",
            f"- **{hints.get('ui_framework', 'Syncfusion Blazor')}** — {hints.get('theme_authority', 'Syncfusion theme provider')}",
            f"- **{hints.get('navigation_surface', 'Dashboard Layout')}** — {hints.get('navigation_surface', 'SfDashboardLayout for workspace organization')}",
            f"- **DI** — {hints.get('di_container', 'Microsoft.Extensions.DependencyInjection wires all services')}",
            f"- **Async init** — {hints.get('async_init_pattern', 'IAsyncDisposable and initialization patterns')}",
            "",
            "## How to Navigate the Active Codebase",
            "1. `Components/` — Blazor components and pages (entry point: `WileyWorkspace.razor`)",
            "2. `Services/` — Application services and business logic",
            "3. `WileyCoWeb.Api/` — ASP.NET Core API controllers and configuration",
            "4. Root-level files — `Program.cs`, project files, configuration",
            "5. `src/` — **ARCHIVED** (WinForms legacy code, not active)",
            "",
            "## Critical Files (read these first)",
        ]

        for item in manifest["critical_files"][:20]:
            lines.append(
                f"- `{item['path']}` — {item.get('reason', item.get('type', ''))}"
            )

        lines += ["", "## Recommended Reading Order"]
        for i, path in enumerate(manifest["recommended_reading_order"][:20], 1):
            lines.append(f"{i}. `{path}`")

        lines += [
            "",
            "## Architecture Summary",
            "| Component | Count |",
            "|-----------|-------|",
        ]
        for k, v in arch["counts"].items():
            lines.append(f"| {k.title()} | {v} |")

        # Add new sections for enhanced analysis
        if arch.get("base_classes"):
            lines += ["", "## Key Base Classes"]
            for base, files in arch["base_classes"].items():
                lines.append(f"### {base}")
                for f in files[:10]:
                    lines.append(f"- `{f}`")

        if arch.get("syncfusion_packages"):
            lines += ["", "## Syncfusion Packages"]
            for pkg in arch["syncfusion_packages"]:
                lines.append(f"- `{pkg}`")

        if arch.get("xaml_controls"):
            lines += ["", "## Top XAML Controls"]
            for control, count in list(arch["xaml_controls"].items())[:10]:
                lines.append(f"- `{control}` ({count} usages)")

        for section, items in [
            ("ViewModels", arch["viewmodels"]),
            ("Panels", arch["panels"]),
            ("Services", arch["services"]),
            ("Controls", arch["controls"]),
        ]:
            lines += ["", f"## {section}"]
            for p in items[:30]:
                lines.append(f"- `{p}`")

        lines += ["", "## Key NuGet Dependencies"]
        for d in manifest["nuget_dependencies"][:25]:
            lines.append(f"- `{d['package']}` v{d['version']}")

        lines += [
            "",
            "## Manifest Stats",
            f"- Total files indexed: **{summary['total_files']}**",
            f"- Files with embedded content: **{summary.get('embedded_files', 0)}**",
            f"- Total source size: **{summary.get('total_size_kb', 0):,.0f} KB**",
            f"- Manifest mode: **{summary.get('manifest_mode', 'unknown')}**",
            "",
            "---",
            "> Auto-generated by `scripts/generate-ai-manifest.py`. Do not edit manually.",
        ]

        brief_path.write_text("\n".join(lines), encoding="utf-8")
        return brief_path

    # ------------------------------------------------------------------
    # Main generate
    # ------------------------------------------------------------------

    def generate_manifest(self) -> Dict[str, Any]:
        """Generate the complete v2.0 manifest."""
        print("🚀 Starting AI manifest generation...")
        overall_progress = ProgressBar(6, "📊 Overall progress")

        repo_info = self._get_repo_info()
        overall_progress.update()
        overall_progress.set_description("📁 Scanning files")

        files = self._scan_files()
        self._files = files
        overall_progress.update()
        overall_progress.set_description("📈 Generating metrics")

        summary = self._generate_summary()
        metrics = self._count_code_metrics()
        overall_progress.update()
        overall_progress.set_description("🔍 Analyzing architecture")

        architecture = self._analyze_architecture()
        overall_progress.update()
        overall_progress.set_description("📦 Parsing dependencies")

        nuget = self._parse_nuget_deps()
        critical_files, reading_order = self._build_critical_and_reading_order()
        overall_progress.update()
        overall_progress.set_description("📂 Building folder tree")

        folder_tree = (
            self._generate_folder_tree()
            if self.config.get("emit_full_tree", False)
            else {}
        )
        overall_progress.update()
        overall_progress.set_description("✅ Finalizing manifest")

        test_coverage = 0.0
        if metrics["total_code_lines"] > 0:
            test_lines = sum(
                1 for f in files if "test" in f["metadata"]["path"].lower()
            )
            test_coverage = round(
                test_lines / max(metrics["total_code_lines"], 1) * 100, 1
            )

        overall_progress.update()
        overall_progress.close()
        print("✨ Manifest generation complete!")

        return {
            "$schema": (
                "https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/"
                "schemas/ai-manifest-schema.json"
            ),
            "manifest_version": "2.1",
            "repository": repo_info,
            # ── v2.0 top-level additions ─────────────────────────────
            "critical_files": critical_files,
            "recommended_reading_order": reading_order,
            "nuget_dependencies": nuget,
            # ─────────────────────────────────────────────────────────
            "license": {"type": "Unknown", "file": None, "detected": False},
            "summary": summary,
            "metrics": {
                "total_lines_of_code": metrics["total_lines_of_code"],
                "total_code_lines": metrics["total_code_lines"],
                "total_comment_lines": metrics["total_comment_lines"],
                "total_blank_lines": metrics["total_blank_lines"],
                "average_complexity": metrics["average_complexity"],
                "test_coverage_percent": test_coverage,
                "test_count": metrics["test_count"],
                "project_metrics": {
                    "windows_forms_complexity": metrics["average_complexity"],
                    "docking_factories_analyzed": True,
                    "estimated_code_to_test_ratio": round(
                        metrics["total_code_lines"] / max(metrics["test_count"], 1), 2
                    ),
                },
            },
            "security": {
                "vulnerable_packages": [],
                "outdated_packages": [],
                "secrets_detected": False,
                "last_security_scan": datetime.now().isoformat(),
                "note": "Security scanning not implemented in this generator",
            },
            "quality": {
                "build_status": "unknown",
                "analyzers_enabled": True,
                "documentation_coverage": 0.0,
                "technical_debt_minutes": 0,
            },
            "architecture": architecture,
            "dependency_graph": {
                "projects": {},
                "nuget_packages": {d["package"]: d["version"] for d in nuget},
                "top_dependencies": [d["package"] for d in nuget[:20]],
            },
            "folder_tree": folder_tree,
            "search_index": [],
            "files": files,
        }

    def save_manifest(self, output_path: Optional[Path] = None) -> None:
        """Generate and save the manifest to a file, then optionally write AI-BRIEF.md."""
        if output_path is None:
            output_path = self.repo_root / "manifest.json"

        manifest = self.generate_manifest()

        with open(output_path, "w", encoding="utf-8") as fh:
            json.dump(manifest, fh, indent=2, ensure_ascii=False)

        legacy_output_path = self.repo_root / "ai-fetchable-manifest.json"
        if legacy_output_path != output_path:
            with open(legacy_output_path, "w", encoding="utf-8") as fh:
                json.dump(manifest, fh, indent=2, ensure_ascii=False)

        size_mb = output_path.stat().st_size / (1024 * 1024)
        print(
            f"Manifest generated: {output_path} "
            f"({self._total_files} files, {self._embedded_count} embedded, {size_mb:.1f} MB)"
        )

        if legacy_output_path != output_path:
            print(f"Legacy manifest generated: {legacy_output_path}")

        if self.generate_context_summary:
            try:
                brief = self._generate_ai_brief(manifest)
                print(f"AI-BRIEF.md generated: {brief}")
            except Exception as e:
                print(f"Error generating AI-BRIEF.md: {e}", file=sys.stderr)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------


def main() -> None:
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Generate AI-optimized manifest for repository"
    )
    parser.add_argument(
        "--compact",
        action="store_true",
        help="Skip content embedding for faster generation on large repos",
    )
    parser.add_argument(
        "--threads",
        type=int,
        default=4,
        help="Number of threads for parallel processing (default: 4, use 0 to disable)",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).parent.parent

    try:
        generator = AIManifestGenerator(repo_root, max_threads=args.threads)

        # Override manifest mode if --compact is specified
        if args.compact:
            generator.manifest_mode = "compact"
            print("Running in compact mode (no content embedding)")

        if args.threads > 0:
            print(f"Using {args.threads} threads for parallel processing")
        else:
            print("Running in single-threaded mode")

        generator.save_manifest()
        print("AI fetchable manifest generated successfully.")
    except Exception as exc:
        print(f"Error: {exc}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
