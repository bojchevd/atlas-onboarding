# Where to look

**Read `REASONING.md` first.** The requirements contradict each other in ways that change
what can be built, and that document is the submission. The code demonstrates it.

## Four files

| File | Why |
|---|---|
| `src/Atlas.Domain/ApplicationStateMachine.cs` | The lifecycle, and the one state that automated code structurally cannot leave |
| `src/Atlas.Domain/MarketConfiguration.cs` | How six markets stay one codebase. An interface per rule, selected by configuration, not a switch |
| `src/Atlas.Onboarding/Program.cs` | The endpoints. The 202, and why it is not the 201 the ticket specifies |
| `tests/Atlas.Tests/` | Five tests, chosen for the things most likely to break |

## The decision I most want to be asked about

**Returning 202 instead of the 201 in `ATLAS-1`.** It is a deliberate refusal to build the
API as written. The ticket asks for a synchronous verdict and puts "anything involving a
human reviewing anything" out of scope; compliance GC-2026-0814 §3 requires exactly that
human review and allows it 48 hours. The reasoning is in `REASONING.md` §1.

If I am wrong about this, most of the rest of the design is wrong with it.

## Please ignore

- `VerificationHostedService` — a loop standing in for a message consumer, so the flow
  runs in one process. It is scaffolding, not a design position. The intended shape is in
  `REASONING.md` under service boundaries.
- The in-memory store. The persistence seam is an interface; the implementation behind it
  is a `ConcurrentDictionary` rather than the SQL Server instance in `platform/`.

Both are in known limitations with what I would do instead.
