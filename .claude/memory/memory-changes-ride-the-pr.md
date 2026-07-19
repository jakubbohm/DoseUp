---
name: memory-changes-ride-the-pr
description: In-repo memory is git-versioned — memory edits are made inside the task's worktree/branch and land in its PR; never left uncommitted in the main worktree.
metadata:
  type: feedback
---

Jakub's rule (2026-07-19), verbatim: "since the memory is git versioned, it's changes should be made before a worktree is created and should be part of the PR." Given after a session did a worktree-based docs task correctly but then wrote the memory update *after* removing the worktree — leaving an uncommitted `.claude/memory/` edit in the main worktree, which at that moment had another session's branch checked out.

**Why:** `.claude/memory/` is committed repo content ([[memory-in-repo]]), so a memory edit is a repo mutation like any other file change — outside the task's branch it pollutes whatever the main worktree has checked out (parallel sessions are real here), and it dodges the PR gates everything else goes through.

**How to apply:** treat memory updates as part of the task, not as post-task housekeeping. Sequence them so they're committed on the task's branch and ride its PR — inside the worktree, before it is removed (never after ExitWorktree). If the workspace is already gone, propose the memory text and ask Jakub where it should land instead of touching the main worktree.
