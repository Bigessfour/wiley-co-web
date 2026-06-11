#!/usr/bin/env python3
"""
RAG query CLI for agents.

Usage examples (run from repo root):
    python -m Scripts.rag.query "AGENTS.md canonical implementations QuickBooks duplicate guard and rate math"
    python -m Scripts.rag.query "How is overhead applied in EnterpriseRateService and DataDashboardPanel?" --k 8
    python -m Scripts.rag.query "Jarvis grounding with WorkspaceKnowledge and vampire impact flags" --kind doc

Returns the most relevant chunks (with source paths) so the agent can cite them and avoid drift.
"""

import argparse
import sys
from pathlib import Path
from typing import Any, Dict, List

import chromadb
from chromadb.config import Settings
from sentence_transformers import SentenceTransformer

REPO_ROOT = Path(__file__).resolve().parents[2]
CHROMA_DIR = REPO_ROOT / ".rag-index" / "chroma"
EMBED_MODEL_NAME = "all-MiniLM-L6-v2"
DEFAULT_K = 6


def load_collections():
    if not CHROMA_DIR.exists():
        print("[rag] No index found. Run: python Scripts/rag/index.py", file=sys.stderr)
        sys.exit(2)
    client = chromadb.PersistentClient(
        path=str(CHROMA_DIR), settings=Settings(allow_reset=False)
    )
    try:
        main_coll = client.get_collection("repo_chunks")
    except Exception:
        main_coll = None
    try:
        rules_coll = client.get_collection("rules")
    except Exception:
        rules_coll = None
    if main_coll is None and rules_coll is None:
        print("[rag] Collections not found. Rebuild index.", file=sys.stderr)
        sys.exit(2)
    return main_coll, rules_coll


def embed_query(text: str) -> List[float]:
    model = SentenceTransformer(EMBED_MODEL_NAME)
    return model.encode([text], show_progress_bar=False, convert_to_numpy=True)[
        0
    ].tolist()


def format_result(i: int, res: Dict[str, Any]) -> str:
    meta = res.get("metadata", {}) or {}
    src = meta.get("source", res.get("id", "unknown"))
    kind = meta.get("kind", "?")
    lang = meta.get("language", "")
    dist = res.get("distance")
    header = f"[{i}] {src} (kind={kind}{', ' + lang if lang else ''})"
    if dist is not None:
        header += f"  dist={dist:.4f}"
    text = (res.get("document") or "").strip()
    return f"{header}\n{text}\n"


def main():
    parser = argparse.ArgumentParser(
        description="Query the local repo RAG index (for agents)."
    )
    parser.add_argument(
        "query",
        help="Natural language question (include 'AGENTS.md' or 'canonical' for rules).",
    )
    parser.add_argument(
        "-k",
        "--k",
        type=int,
        default=DEFAULT_K,
        help="Number of chunks to return (default 6)",
    )
    parser.add_argument(
        "--kind", help="Filter by kind (code, doc, spec, agent-rule, test)"
    )
    parser.add_argument(
        "--path",
        help="Substring filter on source path (e.g. 'EnterpriseRate' or 'src/')",
    )
    parser.add_argument(
        "--rules-only",
        action="store_true",
        help="Only search the high-priority governing/rules collection",
    )
    args = parser.parse_args()

    main_coll, rules_coll = load_collections()
    qvec = embed_query(args.query)

    results: List[Dict[str, Any]] = []

    def do_query(coll, n: int):
        if coll is None:
            return []
        where = {}
        if args.kind:
            where["kind"] = args.kind
        # Chroma where is limited; we do light post-filter on path
        q = coll.query(
            query_embeddings=[qvec],
            n_results=max(n, 12),  # over-retrieve then trim
            where=where or None,
        )
        out = []
        ids = q.get("ids", [[]])[0]
        docs = q.get("documents", [[]])[0]
        metas = q.get("metadatas", [[]])[0]
        dists = q.get("distances", [[]])[0] if q.get("distances") else [None] * len(ids)
        for _id, doc, meta, dist in zip(ids, docs, metas, dists):
            src = (meta or {}).get("source", _id)
            if args.path and args.path.lower() not in src.lower():
                continue
            out.append(
                {
                    "id": _id,
                    "document": doc,
                    "metadata": meta,
                    "distance": dist,
                }
            )
        return out

    if (
        args.rules_only
        or "AGENTS" in args.query.upper()
        or "CANONICAL" in args.query.upper()
    ):
        # Prefer rules collection when the query is about governance
        results = do_query(rules_coll, args.k)
        if not results and rules_coll is not None:
            results = do_query(rules_coll, args.k)

    if not results:
        results = do_query(main_coll, args.k)

    # Also pull a few rules if the main results didn't surface them and query mentions rules/governing
    if (
        rules_coll
        and not args.rules_only
        and any(
            w in args.query.lower()
            for w in ["agent", "rule", "canonical", "prohibit", "drift"]
        )
    ):
        extra = do_query(rules_coll, 3)
        # Merge unique by id
        seen = {r["id"] for r in results}
        for e in extra:
            if e["id"] not in seen:
                results.append(e)

    # Sort by distance if present (lower is better for cosine in chromadb query)
    results.sort(key=lambda r: (r.get("distance") or 1.0))

    results = results[: args.k]

    if not results:
        print("[rag] No relevant chunks found. Try a broader query or rebuild index.")
        return

    print(f"\n=== RAG results for: {args.query} (k={len(results)}) ===\n")
    for i, r in enumerate(results, 1):
        print(format_result(i, r))
    print("=== End RAG results ===")
    print(
        "\nAgent instruction: Cite the [N] source paths above in your reasoning and base decisions on them (combined with direct file reads/grep)."
    )


if __name__ == "__main__":
    main()
