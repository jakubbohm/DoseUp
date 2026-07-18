---
name: copy-paste-commands-in-powershell
description: Commands/scripts Jakub is meant to copy-paste must be PowerShell 7, not bash
metadata:
  type: feedback
---

Any command or script I hand Jakub to **copy and paste and run himself** — runbook steps, setup scripts (e.g. the Service Bus creation command in a spike runbook), README "run it yourself" blocks — must be written in **PowerShell 7 (pwsh)** syntax, not bash.

**Why:** Jakub's shell is PowerShell 7 on Windows. Bash-isms silently break when pasted: `\` line-continuation is not a pwsh continuation (pwsh uses backtick `` ` `` or, better, splatting), `export FOO=bar` / inline `VAR=x cmd` prefixes don't exist (`$env:FOO='value'`), `$(...)` vs `$(...)` quoting differs, `2>/dev/null` → `2>$null`, `~`/path and single-vs-double-quote rules differ. A bash runbook looks fine and fails on paste.

**How to apply:**
- Default all copy-paste command blocks and fenced scripts aimed at Jakub to pwsh 7. Tag the fence ```powershell.
- Multi-line native commands (e.g. `az ...`): use backtick `` ` `` continuation or PowerShell splatting (`$params = @{...}; az ... @params`) — never trailing `\`.
- Env vars: `$env:ConnectionStrings__messaging = '...'` then the command; not `FOO=... cmd`.
- Prefer `.ps1` over `.sh` when providing a runnable script for him; keep a `.sh` alongside only if a Linux/CI path also needs it.
- This is about commands **for Jakub to run**. It does NOT change my own internal Bash tool usage (that runs Git Bash and bash syntax is correct there).
- Applies going forward. Jakub explicitly said **not** to retrofit the existing `spikes/` runbooks (they keep their bash blocks) — this is about new copy-paste commands, not a sweep of existing docs.
