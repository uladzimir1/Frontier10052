# Architecture Decision Records

Implementation decisions that constrain multiple projects or are expensive to reverse must be recorded here as Architecture Decision Records (ADRs).

## Status values

- `Proposed`
- `Accepted`
- `Superseded by ADR-XXXX`
- `Rejected`

## Required first decisions

| ADR | Decision | Status |
|---|---|---|
| ADR-0001 | Blazor hosting model and account boundary | Proposed |
| ADR-0002 | Browser renderer selection and fallback | Proposed |
| ADR-0003 | Save storage and PostgreSQL adoption point | Proposed |
| ADR-0004 | Deterministic random strategy | Proposed |
| ADR-0005 | Simulation clock and time scaling | Proposed |
| ADR-0006 | Content serialization, validation, and versioning | Proposed |
| ADR-0007 | Persistence snapshot and command-journal boundary | Proposed |
| ADR-0008 | Numeric units and precision rules | Proposed |

## ADR template

Copy this template to `ADR-XXXX-short-title.md`.

```markdown
# ADR-XXXX: Title

- Status: Proposed
- Date: YYYY-MM-DD
- Owners: names or roles

## Context

What problem must be decided? Which constraints and evidence matter?

## Decision drivers

- Driver one
- Driver two

## Considered options

### Option A

Benefits, costs, risks, and evidence.

### Option B

Benefits, costs, risks, and evidence.

## Decision

State the selected option precisely.

## Consequences

### Positive

- Consequence

### Negative

- Consequence

### Follow-up work

- Work item

## Validation and reversal trigger

How will the decision be tested, and what evidence would justify revisiting it?
```

## Decision policy

- Prefer measured spikes over preference-based arguments.
- Keep decisions reversible until the vertical slice supplies evidence.
- An ADR does not replace tests, benchmarks, threat models, or API contracts.
- Update or supersede ADRs when reality changes; do not silently contradict them in code.
