---
name: propose-in-prose
description: "Decisions get a prose proposal Jakub replies to freely — never AskUserQuestion option menus framed as \"pick one so I can start\""
metadata: 
  node_type: memory
  type: feedback
  originSessionId: ec1500fc-bebc-4575-971a-0bcd7a55e255
---

When a decision is Jakub's to make (architectural, tooling, process, scoping — per [[user-gates-stage-progression]]), deliver a **proposal in prose**: the solution, the reasoning, trade-offs, alternatives, and a recommendation — then stop and wait for his free-form verdict. Do not present clickable option menus (AskUserQuestion) for such decisions.

**Why:** After a verified investigation (Roslynator brace enforcement, 2026-07-15) I ended with an AskUserQuestion menu of implementation-ready choices; Jakub rejected it: "You had to propose and explain a solution, not starting implementing it. I'm the decision maker, remember?" A menu frames the decision as already reduced to my buttons and signals execution is queued — it takes the framing power from him.

**How to apply:** Investigation and scratch-project verification before proposing is fine (and valued — see [[user-working-style]]). The deliverable is the explained proposal; his reply supplies the verdicts, often with nuance no menu would contain. Implementation starts only after his explicit go.
