#!/usr/bin/env bash
# Convenience wrapper for the simple local RAG (index + query)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$REPO_ROOT"

cmd="${1:-query}"
shift || true

case "$cmd" in
index | build | rebuild)
	python Scripts/rag/index.py "$@"
	;;
query | ask | search)
	python -m Scripts.rag.query "$@"
	;;
*)
	echo "Usage: $0 {index|query} [args...]"
	echo "  $0 index [--force]"
	echo "  $0 query 'your question about AGENTS.md or overhead or Jarvis...'"
	exit 1
	;;
esac
