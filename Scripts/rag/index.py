#!/usr/bin/env python3
"""
Simple local RAG indexer for the Wiley Widget repo (for future agent use).

- Respects .gitignore (via git ls-files + pathspec)
- Structure-aware chunking for code (.cs/.razor) and docs (.md)
- Local embeddings (sentence-transformers)
- ChromaDB persistent store under .rag-index/chroma
- Two collections: "repo_chunks" (general) + "rules" (AGENTS.md + governing docs get priority)

Usage:
    python Scripts/rag/index.py [--force]

After running, use query.py for retrieval.
Re-run after significant changes (culls, new canonical docs, major refactors).
"""

import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Any, Dict, List, Tuple

import chromadb
import pathspec
from chromadb.config import Settings
from sentence_transformers import SentenceTransformer

# --- Config (tune as needed) ---
REPO_ROOT = Path(__file__).resolve().parents[2]
INDEX_DIR = REPO_ROOT / ".rag-index"
CHROMA_DIR = INDEX_DIR / "chroma"
EMBED_MODEL_NAME = "all-MiniLM-L6-v2"  # small, fast, good quality for code+docs
TARGET_CHUNK_CHARS = 2200  # ~500-700 tokens rough; adjust for your embedding model
OVERLAP_CHARS = 400
COLLECTION_NAME = "repo_chunks"
RULES_COLLECTION_NAME = "rules"

# High-value governing files that should be strongly represented (boost into rules collection)
GOVERNING_GLOBS = [
    "AGENTS.md",
    "copilot-instructions.md",
    "docs/wiley-widget-functional-breakdown.md",
    "docs/playwright-ui-test-strategy.md",
    "docs/post-production-operations-handbook.md",
    "docs/quickbooks-desktop-import-guide.md",
    "docs/enterprise-*.md",
    "docs/council-readiness*.md",
]

# Skip these even if git tracks them for the index (binaries, huge data, generated)
EXTRA_SKIP_GLOBS = [
    "Import Data/**/*.xlsx",
    "Import Data/**/*.xls",
    "**/*.png",
    "**/*.jpg",
    "**/*.jpeg",
    "**/*.gif",
    "**/*.pdf",
    "**/*.dll",
    "**/*.exe",
    "**/*.pdb",
    "bin/**",
    "obj/**",
    "node_modules/**",
    "TestResults/**",
    "playwright-report/**",
    ".rag-index/**",
    "publish_output/**",
    "api_output/**",
    "**/__pycache__/**",
    "**/*.pyc",
]


def run_git_ls_files() -> List[str]:
    """Use git ls-files for the authoritative tracked file list (respects .gitignore)."""
    try:
        out = subprocess.check_output(
            ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
            cwd=REPO_ROOT,
            text=True,
        )
        files = [line.strip() for line in out.splitlines() if line.strip()]
        return files
    except Exception as e:
        print(f"[index] git ls-files failed ({e}), falling back to full walk (slower).")
        return []


def load_pathspec() -> pathspec.PathSpec:
    """Load .gitignore rules + our extra skips for additional filtering."""
    gi = REPO_ROOT / ".gitignore"
    patterns = []
    if gi.exists():
        patterns.extend(gi.read_text().splitlines())
    patterns.extend(EXTRA_SKIP_GLOBS)
    # Clean empty / comment lines
    patterns = [p for p in patterns if p and not p.strip().startswith("#")]
    return pathspec.PathSpec.from_lines("gitwildmatch", patterns)


def is_text_file(p: Path) -> bool:
    try:
        with p.open("rb") as f:
            chunk = f.read(4096)
            if b"\0" in chunk:
                return False
        return True
    except Exception:
        return False


def should_include(rel: str, ps: pathspec.PathSpec) -> bool:
    if ps.match_file(rel):
        return False
    p = REPO_ROOT / rel
    if not p.is_file():
        return False
    if not is_text_file(p):
        return False
    # Size guard (very large text files are rare after culls)
    try:
        if p.stat().st_size > 2 * 1024 * 1024:  # 2MB text is suspicious for source
            return False
    except Exception:
        return False
    return True


def read_text(rel: str) -> str:
    p = REPO_ROOT / rel
    return p.read_text(encoding="utf-8", errors="ignore")


# --- Simple structure-aware chunkers (no heavy deps) ---


def chunk_markdown(text: str, source: str) -> List[Dict[str, Any]]:
    """Split on headers first, then recursive char with overlap."""
    chunks = []
    # Split on ## / ### (keep header with following content)
    parts = re.split(r"(?m)^(##+ .+)$", text)
    current = ""
    for part in parts:
        if re.match(r"^##+ ", part):
            if current.strip():
                chunks.extend(_recursive_chunk(current.strip(), source, kind="doc"))
            current = part + "\n"
        else:
            current += part
    if current.strip():
        chunks.extend(_recursive_chunk(current.strip(), source, kind="doc"))
    if not chunks:
        chunks = _recursive_chunk(text, source, kind="doc")
    return chunks


def chunk_code(text: str, source: str, language: str) -> List[Dict[str, Any]]:
    """Prefer splits at class / method / namespace boundaries for C#/Razor/TS/etc."""
    # Common structural markers (order matters — more specific first)
    split_re = re.compile(
        r"(?m)^(namespace |public (class|interface|record|struct) |private (class|interface) |"
        r"protected (class|interface) |class |interface |def |function |# |export (class|interface|const|function) )",
    )
    parts = split_re.split(text)
    # Recombine (the split produces alternating delimiters + content)
    merged = []
    buf = ""
    for i, part in enumerate(parts):
        if split_re.match(part):
            if buf.strip():
                merged.append(buf)
            buf = part
        else:
            buf += part
    if buf.strip():
        merged.append(buf)

    chunks: List[Dict[str, Any]] = []
    for m in merged:
        m = m.strip()
        if not m:
            continue
        chunks.extend(_recursive_chunk(m, source, kind="code", language=language))
    if not chunks:
        chunks = _recursive_chunk(text, source, kind="code", language=language)
    return chunks


def _recursive_chunk(
    text: str,
    source: str,
    kind: str,
    language: str = "",
    max_chars: int = TARGET_CHUNK_CHARS,
) -> List[Dict[str, Any]]:
    """Recursive character splitter with sensible separators + overlap."""
    if len(text) <= max_chars:
        return [{"text": text, "source": source, "kind": kind, "language": language}]

    seps = ["\n\n", "\n", ". ", " ", ""]
    for sep in seps:
        if sep in text:
            pieces = text.split(sep)
            out = []
            current = ""
            for piece in pieces:
                candidate = (
                    (current + sep + piece).strip() if current else piece.strip()
                )
                if len(candidate) > max_chars and current:
                    out.append(current.strip())
                    current = piece
                else:
                    current = candidate
            if current.strip():
                out.append(current.strip())

            # Add overlap between consecutive chunks
            final = []
            for i, c in enumerate(out):
                if i > 0 and OVERLAP_CHARS > 0:
                    prev = final[-1]["text"] if final else ""
                    overlap = (
                        prev[-OVERLAP_CHARS:] if len(prev) > OVERLAP_CHARS else prev
                    )
                    c = (overlap + "\n" + c).strip()
                final.append(
                    {"text": c, "source": source, "kind": kind, "language": language}
                )
            # Guard against pathological single huge chunk
            if len(final) == 1 and len(final[0]["text"]) > max_chars * 1.5:
                # Hard split
                t = final[0]["text"]
                mid = len(t) // 2
                final = [
                    {
                        "text": t[:mid],
                        "source": source,
                        "kind": kind,
                        "language": language,
                    },
                    {
                        "text": t[mid:],
                        "source": source,
                        "kind": kind,
                        "language": language,
                    },
                ]
            return final
    # Fallback
    return [
        {"text": text[:max_chars], "source": source, "kind": kind, "language": language}
    ]


def get_language(rel: str) -> str:
    ext = Path(rel).suffix.lower()
    if ext in {".cs", ".razor"}:
        return "csharp"
    if ext in {".ts", ".tsx", ".js", ".jsx"}:
        return "typescript"
    if ext == ".py":
        return "python"
    if ext in {".sh", ".bash"}:
        return "shell"
    if ext in {".md", ".markdown"}:
        return "markdown"
    return "text"


def is_governing(rel: str) -> bool:
    from fnmatch import fnmatch

    for g in GOVERNING_GLOBS:
        if fnmatch(rel, g):
            return True
    return False


def main(force: bool = False):
    print(f"[index] Repo root: {REPO_ROOT}")
    print(f"[index] Index dir: {INDEX_DIR}")

    INDEX_DIR.mkdir(parents=True, exist_ok=True)

    # Load files
    raw_files = run_git_ls_files()
    if not raw_files:
        # Fallback walk
        raw_files = [
            str(p.relative_to(REPO_ROOT)) for p in REPO_ROOT.rglob("*") if p.is_file()
        ]

    ps = load_pathspec()
    files = [f for f in raw_files if should_include(f, ps)]
    print(f"[index] {len(files)} candidate files after filters")

    # Prepare Chroma (reset on force or to keep things clean for re-index)
    client = chromadb.PersistentClient(
        path=str(CHROMA_DIR), settings=Settings(allow_reset=True)
    )
    for name in (COLLECTION_NAME, RULES_COLLECTION_NAME):
        try:
            client.delete_collection(name)
        except Exception:
            pass

    coll = client.get_or_create_collection(
        COLLECTION_NAME, metadata={"hnsw:space": "cosine"}
    )
    rules_coll = client.get_or_create_collection(
        RULES_COLLECTION_NAME, metadata={"hnsw:space": "cosine"}
    )

    # Embedder (lazy download on first run)
    print(
        f"[index] Loading embedding model: {EMBED_MODEL_NAME} (first run downloads ~20-80MB)"
    )
    embedder = SentenceTransformer(EMBED_MODEL_NAME)

    all_chunks: List[Dict[str, Any]] = []
    rules_chunks: List[Dict[str, Any]] = []

    for rel in files:
        try:
            text = read_text(rel)
            if not text.strip():
                continue
            lang = get_language(rel)
            if lang == "markdown" or rel.endswith(".md"):
                chs = chunk_markdown(text, rel)
            elif lang in ("csharp", "typescript", "python", "shell"):
                chs = chunk_code(text, rel, lang)
            else:
                chs = _recursive_chunk(
                    text, rel, kind="doc" if "doc" in rel.lower() else "text"
                )

            for idx, c in enumerate(chs):
                # Robust unique ID (prevents duplicates across runs/collections)
                c["id"] = f"{rel}#{idx}#{c.get('kind', 'chunk')}"
                c["language"] = lang
            all_chunks.extend(chs)

            if is_governing(rel):
                rules_chunks.extend(chs)
        except Exception as e:
            print(f"[index] skip {rel}: {e}")

    print(f"[index] Produced {len(all_chunks)} chunks ({len(rules_chunks)} governing)")

    if not all_chunks:
        print("[index] No chunks — nothing to index.")
        return

    # Embed + upsert (batch for speed)
    def upsert(coll_obj, chunk_list: List[Dict[str, Any]], batch_size: int = 128):
        for i in range(0, len(chunk_list), batch_size):
            batch = chunk_list[i : i + batch_size]
            texts = [b["text"] for b in batch]
            ids = [b["id"] for b in batch]
            metas = [
                {
                    "source": b["source"],
                    "kind": b.get("kind", "text"),
                    "language": b.get("language", "text"),
                }
                for b in batch
            ]
            embs = embedder.encode(
                texts, show_progress_bar=False, convert_to_numpy=True
            ).tolist()
            coll_obj.upsert(ids=ids, embeddings=embs, documents=texts, metadatas=metas)
        print(f"[index] Upserted {len(chunk_list)} into {coll_obj.name}")

    upsert(coll, all_chunks)
    if rules_chunks:
        upsert(rules_coll, rules_chunks)

    print("[index] Done. Index location:", CHROMA_DIR)
    print(
        '[index] Tip: python -m Scripts.rag.query "AGENTS.md rules + overhead allocation"'
    )


if __name__ == "__main__":
    force = "--force" in sys.argv
    main(force=force)
