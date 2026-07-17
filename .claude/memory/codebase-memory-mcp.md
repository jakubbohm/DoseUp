---
name: codebase-memory-mcp
description: "This repo's graph-first code-discovery MCP; on v0.9.0 the default `full` index mode crashes — use `moderate`/`fast`"
metadata: 
  node_type: memory
  type: reference
  originSessionId: e34dff81-fb36-41e9-9a96-44a8b464583f
---

DoseUp uses **codebase-memory-mcp** as its code-discovery layer (adopted 2026-07-15). A SessionStart hook mandates its graph tools (`search_graph`/`trace_path`/`get_code_snippet`/`search_code`/…) over grep/Read for code exploration; fall back to Grep/Glob/Read only for text/config/non-code.

- **Install:** self-contained ~270 MB binary at `C:/Users/jakub/.local/bin/codebase-memory-mcp.exe` (not uv/pipx-managed). Source + updates = GitHub `DeusData/codebase-memory-mcp` releases; `codebase-memory-mcp update -y` self-updates. Currently **v0.9.0**.
- **Config:** server declared once in global `~/.claude/.mcp.json`; per-project it's the only default MCP via `.claude/settings.json` (`deniedMcpServers` + `disableClaudeAiConnectors`). Index is a local cache under `~/.cache/codebase-memory-mcp/` — nothing committed in-repo.
- **Indexing modes:** `full` = all files + similarity/semantic (779 nodes / 1390 edges here); `moderate`/`fast` = filtered set (633 / 1239). `full` is the default and worked fine via the MCP `index_repository` tool.
- **Post-update gotcha (observed on the 0.8.1 → 0.9.0 update):** running `codebase-memory-mcp cli index_repository` in default `full` mode **immediately after the self-update** crashed three times ("Indexing worker crashed on a file", empty worker log; also "The system cannot find the path specified"). `moderate`/`fast` worked from the CLI, and `full` worked cleanly once the tool process was relaunched (MCP reconnect). Lesson: **after updating the binary, restart the tool process before indexing** — the freshly-swapped exe seems unable to spawn its worker until a clean start. Also: updating deletes/rebuilds all existing index caches (schema change) and can't overwrite the exe while any `codebase-memory-mcp.exe` is running (Windows file lock) — update from a terminal outside a live Claude session.

Related: [[doseup-project-state]].
