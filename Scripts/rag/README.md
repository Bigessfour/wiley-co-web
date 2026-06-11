# Local RAG for Agents (Wiley Widget)

Simple Python + ChromaDB implementation to give agents reliable retrieval over the repo's governing documents and core code.

See the parent `docs/rag-for-agents.md` for the full plan, best-practice rationale (2026 hybrid agentic + vector), chunking approach, and mandatory usage instructions in AGENTS.md.

## Quick commands (from repo root)

```bash
# Install
cd Scripts/rag && python -m pip install -r requirements.txt && cd ../..

# (Re)build index after changes
python Scripts/rag/index.py
# or
Scripts/rag/rag.sh index

# Query (the agent entrypoint)
python -m Scripts.rag.query "AGENTS.md canonicals + overhead allocation + EnterpriseRateService"
python -m Scripts.rag.query "QuickBooks import duplicate guard and 4-enterprise routing"
```

The index is stored in `.rag-index/` (gitignored).

Rebuild when AGENTS.md, core services, or major docs change.
