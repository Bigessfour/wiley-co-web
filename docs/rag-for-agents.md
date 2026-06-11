# RAG for Agents — Wiley Widget Repo Context (Simple Local Python + Chroma)

**Purpose**: Give coding agents (Grok CLI, Cursor, Claude Code, etc.) reliable, queryable access to the _full repo context_ — especially the governing rules in AGENTS.md, canonical implementations, core loop code (import → categorize/allocate overhead → analyze net/vampire → viz Syncfusion → Jarvis), docs, specs, and tests — so they follow established patterns and **prevent drift**.

This is **not** a replacement for the agent's native tools (read_file, grep, list_dir, terminal builds/tests, Playwright). It is a **mandatory first retrieval step** for context before planning or editing in any area.

## Why this RAG (research-backed, 2026)

From current best practices:

- Pure vector RAG on _code_ is often de-emphasized by top agentic tools (Cursor, Claude Code, Aider etc.) in favor of agentic/lexical search (grep, AST, exact file reads) because embeddings lose syntactic structure, dependencies, and exact matches.
- Vector RAG shines for **prose + governing knowledge**: AGENTS.md rules, "why" decisions in docs, high-level architecture, test strategies, canonical patterns.
- **Recommended hybrid**: RAG (semantic over docs + key code chunks) + direct tools (grep/read for implementation details) + verification (builds, HighRisk tests, Playwright).
- Chunking: Start with recursive + structure-aware (headers for .md; class/method boundaries for .cs/.razor). ~400-800 token chunks, 10-20% overlap. Metadata is king (path, kind, language).
- Vector DB for _simple local Python_: ChromaDB (embedded/persistent, excellent metadata filtering, minimal deps). Local embeddings (sentence-transformers) for privacy/speed/no cost.
- To prevent drift: Force agents to retrieve + cite relevant chunks (especially AGENTS + core loop files) on every non-trivial task. Treat retrieved context as authoritative.

This implementation is deliberately **simple** (no LangChain/LlamaIndex, ~3 Python packages, one persistent Chroma dir), local-only, and aligned with the repo's existing Python scripts (`generate-ai-manifest.py`, data inspection scripts) and post-cull focus on the 5 core processes.

## Architecture (simple)

- **Ingestion** (`Scripts/rag/index.py`):
  - Discovers files via `git ls-files` (respects .gitignore) + explicit filters.
  - Skips: binaries, `Import Data/*.xlsx` (large), `bin/`, `obj/`, `node_modules/`, `TestResults/`, `.rag-index/`, build artifacts, very large files.
  - Chunking (lightweight, no tree-sitter for v1 simplicity):
    - Markdown/docs: Split on `##` / `###` + recursive char splitter.
    - Code (.cs, .razor, .ts, .py, .sh): Split on common structural markers (`class `, `public `, `private `, `namespace`, `def `, `# `, etc.) + recursive.
    - Small files (< ~1k tokens): often kept as single chunk.
    - Overlap + metadata (source path, kind=`code|doc|spec|agent-rule|test`, language, approx line range).
  - Special handling: AGENTS.md, key canonical sections, `docs/wiley-widget-functional-breakdown.md`, `docs/playwright-ui-test-strategy.md`, `docs/post-production-operations-handbook.md`, core services (EnterpriseRateService, QuickBooksRoutingService, WorkspaceKnowledgeService, Jarvis\*, WorkspaceState, DataDashboardPanel etc.) get priority boosting (separate "rules" collection or `priority` metadata).
  - Embeddings: Local `sentence-transformers` (all-MiniLM-L6-v2 or BGE small; downloads once, cached in `~/.cache`).
  - Store: ChromaDB persistent client at `.rag-index/chroma` (two collections: `repo_chunks` + `rules` for high-priority governing docs).

- **Retrieval** (`Scripts/rag/query.py` or `python -m Scripts.rag.query "..."`):
  - Takes natural language question (e.g. "how is overhead applied and what are the canonical rate math rules?").
  - Optional filters (e.g. `--kind agent-rule --path src`).
  - Returns top-k (default 6-8) most relevant chunks with:
    - Source file path + line hints
    - Kind / language
    - Score
    - Full chunk text (ready to paste into agent context)
  - Output is human + agent friendly (can be piped or used directly in prompts).

- **Usage for agents (mandatory)**:
  1. Before significant exploration, planning, or edits in an area, run the query tool for "the relevant component + AGENTS.md rules + canonical implementations + core loop".
  2. Incorporate the returned chunks into reasoning.
  3. Cite sources (e.g. "per AGENTS.md:40 and EnterpriseRateService.cs:XX...").
  4. Still use native tools (grep/read_file/terminal) for precise implementation details and verification.
  5. After changes: run the smallest HighRisk test set + relevant Playwright specs.

- **Rebuild**: Run the index script whenever the repo changes substantially (post big refactors, new docs, major feature). Index is fast on the culled repo (~hundreds of files after AWS bloat removal).
- **Persistence**: `.rag-index/` is gitignored (like `TestResults/`, manifests, local settings). Rebuild on new machine or after large changes. Small enough to be cheap to regenerate.

## Files added / changed

- `Scripts/rag/requirements.txt`
- `Scripts/rag/index.py`
- `Scripts/rag/query.py`
- `Scripts/rag/rag.sh` (convenience wrapper)
- `.gitignore` (add `.rag-index/`)
- `docs/rag-for-agents.md` (this file)
- Updates to `AGENTS.md` (new "RAG Context..." section + "mandatory" language)
- Updates to `copilot-instructions.md`
- (Optional) Minor note in `README.md` or `AI-BRIEF.md`

## Quick start (for humans + agents)

```bash
# 1. One-time setup (in repo root)
cd Scripts/rag
python3 -m pip install -r requirements.txt
cd ../..

# 2. Build / refresh the index (run after major changes or first time)
python Scripts/rag/index.py

# 3. Query (examples for core loop + drift prevention)
python -m Scripts.rag.query "AGENTS.md canonical implementations and prohibited changes, especially QuickBooks and rates"
python -m Scripts.rag.query "How is TownOverheadPercent applied? Show EnterpriseRateService helpers and AppSettings + DataDashboard usage"
python -m Scripts.rag.query "Jarvis / WorkspaceAiAssistantService grounding with WorkspaceKnowledgeService and net contribution / vampire flags"
python -m Scripts.rag.query "QuickBooks import duplicate guard and routing to the exact 4 enterprises"
python -m Scripts.rag.query "Required testing workflow and HighRisk gates for changes"
```

The query tool prints nicely formatted chunks you can copy into your next prompt or reasoning.

## Best practices applied in this implementation

- **Chunking**: Recursive + structure-aware delimiters (not naive fixed lines). Special rules collection for governance docs.
- **Metadata + filtering**: Rich metadata enables precise retrieval (e.g. only agent rules or only import-related code).
- **Local & private**: Chroma + sentence-transformers, no cloud calls for embeddings or retrieval.
- **Hybrid with tools**: Explicitly documents that RAG augments (does not replace) read_file/grep/builds/Playwright.
- **Drift prevention**: "MUST retrieve + cite" language modeled on successful agent workflows (explore/plan/verify loops, persistent CLAUDE.md-style rules, verification gates).
- **Leverage existing**: Complements `generate-ai-manifest.py` (static smart dump for full-context when needed) and our strong native tools + test gates.
- **Simplicity & maintainability**: Minimal deps, pure Python where possible, fits existing Scripts/ Python ecosystem. Easy to evolve (add tree-sitter later, switch to pgvector, add reranker, etc.).

## Future enhancements (not in v1)

- AST-aware chunking (tree-sitter for perfect C# functions/classes).
- Hybrid lexical + vector (add simple ripgrep pre-filter).
- pgvector in the existing docker postgres (unify with app data).
- Automatic re-index hook or file watcher (for live agent sessions).
- Integration with `generate-ai-manifest.py` to also emit high-value full chunks.
- Evaluation script (sample queries + golden relevant files, measure recall).
- MCP server wrapper so agents can call RAG as a tool.

## Maintenance

- Rebuild index after culls, big refactors, or doc updates that change canonical behavior.
- Prune `.rag-index/` if it grows (rare for this focused repo).
- Update this doc + AGENTS.md if the RAG process or chunking strategy changes.

This gives agents persistent, queryable "institutional memory" of the repo's established methods while keeping everything local, simple, and focused on the import → categorize → allocate → analyze → viz → Jarvis core.

See also: AGENTS.md (the source of truth that should be heavily represented in the index), `docs/wiley-widget-functional-breakdown.md`, and the HighRisk test filters.
