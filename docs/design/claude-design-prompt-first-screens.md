# Claude Design prompt — first screens iteration

**Status:** ready to run · **Produced:** 2026-07-18 · **Owner:** Jakub Bohm

The input prompt for the first Claude Design iteration (the mockup step required by
[ADR-0004-delivery-and-process](../adr/0004-delivery-and-process.md) for UI-heavy changes). Derived from a
design interview with Jakub on 2026-07-18; his decisions embedded here: due-first home
with adherence below · two-view Schedules (Upcoming forward log / All by substance) ·
shared-skeleton detail/edit (read-optimized view mode) · DayArc sun-path time
visualization · daypart hue system ("the day, instrumented" — reference aesthetic
translated from a 2026 web-platform demo Jakub supplied) · schedule-type taxonomy
(repeats × ends) · agentic natural-language quick-add · notification-landing Dose action
screen.

Amended same day after the first Claude Design run's clarifying questions. Jakub's own
words (the notes field): the primary animated direction **auto-themes with time of day**
— no manual light/dark toggle; manual themes apply only to the dial-down variant — and
DayArc/input gained the **sun-scrub** interaction (dragging a time dot live-retunes the
ambient sky). His other answers were closest-offered options, not his words, and he
refined them afterwards: scope = everything at **full quality** — best possible usability
as the foundation for future development outranks time/cost savings (the offered "broad,
less polish per screen" trade-off explicitly rejected); fully interactive prototype, not
wireframes (the specific interaction enumeration is this prompt's specification, not
Jakub's); ambient dayparts varied across frames (Today additionally at both poles);
monogram avatars.

Amended 2026-07-18 (stack): Jakub's verdict — the implementation styles with **Tailwind CSS
v4** ([ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md)); the design-system
rules below now say so — deliberate prompt steering: documented output fidelity comes from
a linked codebase / pushed design system ([conventions/design.md § 3](../conventions/design.md)),
which this greenfield run doesn't have yet. Radix was mockup direction at this point —
decided 2026-07-19 (next amendment).

Amended 2026-07-19 (floor + primitives): Jakub's verdicts — the support floor is the
**latest iOS** ("no one with anything older will ever use it"; the 16.4 floor was a prior
session's hedge, never his — NFR-1 updated), and **Radix Primitives** is the decided
behavior layer behind our own components ([ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md);
Base UI the named fallback). [#26](https://github.com/jakubbohm/DoseUp/issues/26) stays open for data & state.

Scope notes: the screens design ahead of the roadmap (schedules are M2; the roadmap's
first planned mockup was the M1 logging UI) — a deliberate direction-setting iteration.
The schedule-type taxonomy, postpone durations, and agentic parsing exceed FR-10/FR-12's
current wording; requirements/spec updates are separate steps Jakub kicks off. The React data & state stack is an open design decision
(tracked in [#26](https://github.com/jakubbohm/DoseUp/issues/26)), as are the design personas
([#35](https://github.com/jakubbohm/DoseUp/issues/35)); "use Radix", mockup direction when written, became the
decided behavior layer on 2026-07-19 ([ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md)).

Paste the block below into Claude Design verbatim.

---

```markdown
# DoseUp — first design iteration: Today, Schedules (2 views), Schedule detail/edit, Dose action

## Product context
DoseUp is a private, invite-only medication & supplement dose tracker (installable mobile
PWA) for one family-and-friends circle. Users log scheduled doses (one tap from a push
notification or the Today view), log ad-hoc doses, and manage per-substance schedules —
including non-medication routines (teeth cleaning). One account holds multiple profiles
(parent + child); everything on screen belongs to the active profile. Health-adjacent, so
calm and trustworthy — but also deliberately a showcase of modern (2026) UI craft. Not a
medical device; no clinical-advice UI.

## Deliverables
1. **Today (home)** — due & overdue first, adherence stats below
2. **Schedules** — one screen, two views via segmented control: **Upcoming** / **All**
3. **Schedule detail/edit** — one screen; view and edit modes share one skeleton
4. **Dose action** — the screen a push-notification tap lands on
5. **Agentic quick-add** — natural-language schedule creation flow (3 states)
6. **Component sheet** — every reusable component + tokens, at the day AND night poles

Mobile-first at 390×844. No desktop this round. **Scope: everything, at full quality** —
all six deliverables with their listed states, each done properly. Take the time it
needs: the optimization target is the best possible usability as a foundation for the
development that follows, never time or token savings. **Build it fully interactive** —
a working prototype, not wireframes or annotated stills; the interactions specified in
Motion below (and the sun-scrub) actually run. Theming is automatic (see Art direction):
vary the ambient daypart across frames to spread the range — and show Today at both the
day and night poles.

## Art direction — "the day, instrumented"
Reference feel: editorial-technical 2026, OKLCH-native, alive — NOT flat 2020 cards, NOT
bubbly glassmorphism, NOT Material sameness.

**One living hue.** The entire palette derives from a single OKLCH hue token that follows
time of day: dawn indigo-gold → morning gold → midday blue → evening amber → night deep
indigo. Accent = `oklch(0.8 0.15 var(--hue))`-style derivation; surface tints, hairlines,
and glass all derive from it via relative color syntax and `color-mix()`. The app quietly
knows what part of day it is.
**Exception (hard rule):** status colors are fixed semantic tokens that NEVER drift with
the ambient hue — `--color-status-due`, `--color-status-overdue` (warm red),
`--color-status-taken` (green), `--color-status-skipped` (muted). "Am I late?" must look
identical at 08:00 and 20:00.

**Daypart system.** Five daypart tokens — dawn · morning · midday · evening · night —
each with a hue, a tiny glyph (sunrise, sun, high sun, sunset, moon), and a soft gradient
tint. Used everywhere a time appears: DayArc sky, Upcoming time-group headers, TimeChips,
Dose action ambience. One system, applied consistently.

**Surfaces:** translucent panels with backdrop blur over the ambient sky, 1px hairlines
(`color-mix` of ink into transparent), moderate radii (~12–16px) with squircle
`corner-shape` on key controls, occasional corner-tick bracket detail on featured cards,
subtle grain so nothing is sterile-flat, soft accent glow on the active/due element.

**Type:** three roles — an expressive variable serif with optical sizing (Fraunces-like)
for display numerals and headings, with italic accent moments; a clean humanist sans for
body (≥16px, mixed-age audience); mono with tabular numerals for times, doses, and
uppercase letterspaced micro-labels/eyebrows.

**Motion:** spring `linear()` easing with real overshoot, `@starting-style` entrances,
scroll-driven reveals (blur-to-sharp), view-transition morphs (list card → detail),
checkmark morph on dose-taken with the card settling into Done and the arc node filling.
Fast and subtle — used twice a day, every day; delight never slows the loop. Respect
`prefers-reduced-motion`.

**Theming — automatic, not toggled.** The primary direction has NO manual light/dark
toggle: appearance follows time of day continuously. Daylight hours render as daylight
paper (warm off-white, pastel sky, deeper accent for contrast); night hours as luminous
night (glow, stars on the arc, deep indigo space — never gray boxes); dawn/dusk blend
between the poles. Both poles are fully designed, neither is an inversion of the other.
Vary the ambient daypart across frames (e.g., Today at morning, Upcoming at midday, Dose
action at night); show Today at both poles. (A `prefers-color-scheme`/manual override is
an implementation question, out of scope here.)

**Calibration frames:** render Today twice more — "dial-down" (calm classic: minimal
effects, static background, conventional user-selected light/dark themes — the only
variant with a theme toggle) and "dial-up" (maximum expressive) — so we can pick how
far to push.

**Engineering note:** the support floor is the **latest iOS Safari / Android Chrome**
(NFR-1 — the circle keeps devices current; no backward-compatibility tax). Use the
cutting-edge CSS directly; graceful degradation is owed only where WebKit hasn't shipped
a feature at all (e.g. `corner-shape` as of 2026-07) — nothing may *break* without it.
Mockups may use it all.

## Design-system rules (this seeds a real codebase)
- Behavior maps to **unstyled Radix Primitives** (Dialog/Sheet, Tabs, Switch, Slider,
  Checkbox, DropdownMenu, Toast, Progress). No Radix Themes look, no off-the-shelf theme.
- **Tokens as CSS custom properties**, semantic names (`--hue-ambient`, `--color-bg-*`,
  `--color-text-*`, `--color-accent`, `--color-status-{due,overdue,taken,skipped}`,
  `--daypart-{dawn,morning,midday,evening,night}`, `--space-*`, `--radius-*`, `--font-*`,
  `--shadow-*`, `--blur-*`). Light/dark are two values per token. The implementation
  stack is **Tailwind CSS v4**: static tokens will live in `@theme` and become utilities;
  runtime-varying tokens (`--hue-ambient`) stay plain custom properties that utilities
  reference. Style with Tailwind utility classes where they express it; bespoke CSS only
  for what utilities can't say (the cutting-edge effects above).
- **Every element is an instance of a named reusable component**: `AppBar`, `TabBar`,
  `SegmentedControl`, `ProfileSwitcher` (monogram avatars: initial + per-profile accent
  ring — no photos), `DayArc`, `DoseCard`, `TimeChip`,
  `DaypartHeader`, `DayPatternChips`, `RepeatsSelector`, `EndsSelector`, `AdherenceRing`,
  `AdherenceStrip`, `ScheduleCard`, `SubstanceGroupHeader`, `Badge`, `Button`
  (primary/secondary/ghost/destructive), `FAB`, `Sheet`, `Stepper`, `DateField`,
  `TimeField`, `MagicInput` (agentic), `EmptyState`. No one-off styling.

## Signature element: DayArc
Time of day as a **sun path from dawn to dusk**: arc over a horizon line, time mapped to
position, sun marker at "now", sky gradient keyed to the daypart hues; hours before
~06:00 / after ~21:00 live on horizon extensions with a moon treatment. Dark theme = the
night-sky rendering of the same geometry. Variants:
- **DayArc/hero** (Today): today's doses as nodes — taken = filled check, due-now =
  gentle pulse + glow at the sun, overdue = alert-tinted behind the sun, upcoming =
  outlined ahead. Tapping a node highlights its card below.
- **DayArc/compact** (ScheduleCard, detail): small static arc, dots at scheduled times.
- **DayArc/input** (edit mode): draggable dots, 5-min snapping, numeric fields
  alongside. **Sun-scrub:** while dragging, the dot becomes a small sun (moon past dusk)
  and the screen's ambient sky follows it live — dragging from AM (left) toward PM
  (right) sweeps the background morning gold → midday blue → evening amber → night
  indigo; on release it eases back to the real current daypart.
Hard rule: the arc never carries information alone — always paired with TimeChips.

## Schedule type system (drives the edit form and all sample data)
Two orthogonal choices:
- **Repeats:** fixed times daily · fixed times on weekdays · every N hours (interval
  chain — next due chains from the actual take time; annotate this) · every N
  days/weeks/months · as-needed (PRN: never due; optional max-per-day + min-gap rails)
- **Ends:** open-ended · until date · after N doses taken (show "9 of 24 · ends ~Sun"
  progress on cards and detail)
The **RepeatsSelector swaps the fields beneath it** — design all five states. Cyclic
on/off, tapering, meal-anchored times are future types: exclude them, leave no dead UI.

## Screens

### 1. Today (home)
- AppBar: ProfileSwitcher left (monogram avatars "J" and "E" for profiles "Jakub" and
  "Ella", distinct accent rings), date, overflow menu (no theme toggle — theming is
  automatic).
- DayArc/hero under a subtle living ambient-sky background.
- Dose list in time order: **Overdue** (1 item, prominent Take / Skip — open app + one
  tap logs it), **Coming up**, **Done today** (collapsed). Interval-chain items show
  "~16:00 · 8h after last".
- **Adherence below**: 7-day AdherenceRing (% taken), streak, mini week strip
  (taken/skipped/missed). Encouraging, never judgmental.
- FAB "+ Log dose" (ad-hoc). TabBar: Today · Schedules · History · Settings (last two
  are targets only).
- States: default (1 overdue, 2 upcoming, 2 done) AND **all-done** (arc complete, gentle
  celebration — restrained, calm, not gamey).

### 2. Schedules — segmented: Upcoming | All
- **Upcoming** (forward log, read-only planning): next ~48h grouped by day → time slot.
  Each time-slot group carries a **DaypartHeader**: daypart glyph + hue-tinted band +
  mono time — morning/midday/evening/night are visually distinct at a glance:
  "Today ⏾ 21:00 — Magnesium, Amoxicillin", "Tomorrow ☀ 08:00 — D3, Omega-3, preventer".
  Chained doses annotated as approximate (~16:00).
- **All** (definitions, grouped by substance): SubstanceGroupHeader + its ScheduleCards:
  DayArc/compact + TimeChips, repeat pattern, end condition ("open-ended", "ends 21 Jul",
  "9 of 24 taken"), Badges (Paused, Ending soon). Include the teeth-cleaning routine —
  schedules aren't only meds.
- "+ New schedule" primary action; **MagicInput** ("Describe it…") docked at top of All.
- Empty state designed.

### 3. Schedule detail/edit — one screen, two modes
- **View (default, read-optimized):** hero (substance, strength, amount — display serif),
  DayArc/compact + TimeChips, repeat + end summary line, **this schedule's adherence**
  (14-day strip, %, streak), **next 3 occurrences** (daypart-tinted), secondary actions
  (Pause, End…). Edit in AppBar.
- **Edit:** SAME skeleton; values morph into controls in place — amount → Stepper,
  times → DayArc/input + TimeFields, repeats → RepeatsSelector (all five states),
  ends → EndsSelector. Adherence/upcoming collapse away; sticky Save/Cancel bar. Show
  both modes; the morph runs interactively.
- "New schedule" = edit mode, empty (mention only). If the shared-skeleton morph fights
  the content, counter-propose exactly one alternative side-by-side and argue it.

### 4. Dose action (notification landing)
- Ambient background = the daypart of the scheduled dose.
- Hero: substance, strength, amount, scheduled time — plus the **profile monogram
  avatar with its accent ring, prominent** (a child's dose must be unmistakable).
- One huge **Take now** button; beneath it, quiet "Log a different time" (defaults to
  now; compact adjuster for backdating).
- Quieter **Postpone** row: chips 30 min · 1 h · 4 h · custom.
- **Skip** as ghost action at the bottom.
- Must work at a glance, half-asleep, one-handed.

### 5. Agentic quick-add (3 states)
1. **MagicInput active** — placeholder: "Take Amoxicillin every 8 hours starting today
   at 16:00 until the pack of 24 is finished".
2. **Parsing** — shimmer over a draft-form skeleton.
3. **Parsed draft** — the SAME edit-form components pre-filled; parsed fragments
   highlighted and mapped to their fields; ambiguity resolved via inline question chips
   ("Which strength — 250 mg / 500 mg?"); explicit **Confirm** saves. The agent never
   saves on its own.

## Content & conventions
- English UI, 24-hour times, dates like "21 Jul".
- Sample regimen: Budesonide inhaler 200 µg (asthma preventer — daily 08:00 + 20:00,
  open-ended) · Vitamin D3 2000 IU (daily 08:00) — the overdue item · Omega-3 1000 mg
  (Mon/Wed/Fri 08:00) · Amoxicillin 500 mg (every 8 h from 16:00, ends after 24 doses —
  9 taken) · Magnesium 375 mg (daily 21:00) · Salbutamol inhaler (as-needed, max 8/day)
  · "Brush teeth" routine (07:30 + 21:30, no amount) · ad-hoc Ibuprofen 400 mg in Done.
- Accessibility: WCAG AA contrast across the entire ambient range — day, night, and the
  dawn/dusk blends between them (glass surfaces included), touch targets
  ≥44px, status never color-only (icon + label), arc always paired with text times,
  daypart tint never the sole group indicator (glyph + text label always present),
  `prefers-reduced-motion` respected.

## Out of scope
History screen, Settings, onboarding/auth, medical-disclaimer placement, notification
system UI itself, desktop layouts, cyclic/taper/meal-anchored schedule types.
```
