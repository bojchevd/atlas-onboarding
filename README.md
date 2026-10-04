# Atlas Onboarding

## What this is

A backend slice of Atlas, a customer-onboarding platform for a banking group operating in six markets.
It takes an application from draft through submission and asynchronous sanctions screening to its
market's activation rule, and refers a possible sanctions match to a manual review that no automated
code can leave.

**Read [`REASONING.md`](REASONING.md) first.** The requirements contradict each other in ways that change
what can be built, and that document explains which way each contradiction was resolved.
[`WHERE-TO-LOOK.md`](WHERE-TO-LOOK.md) points from each claim to the code that demonstrates it, and
[`docs/atlas-onboarding-bpmn.svg`](docs/atlas-onboarding-bpmn.svg) is the process diagram.

## Prerequisites

- .NET 9 SDK
- Docker Desktop

Nothing else.

## Running it

All commands run from the repository root. Use three terminals.

**Terminal 1: infrastructure.**

```
docker compose -f platform/docker-compose.yml up -d
docker compose -f platform/docker-compose.yml ps
```

Working looks like `platform-sql-1` as `Up (healthy)` (SQL Server takes about 30 seconds to become
healthy) and `platform-seq-1` as `Up`, with Seq's UI at http://localhost:5341. The service does not use
either yet; see [What is not built](#what-is-not-built).

**Terminal 2: the service.** Leave it running.

```
dotnet run --project src/Atlas.Onboarding
```

Working looks like:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5287
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

`curl http://localhost:5287/health` returns `ok`.

**Terminal 3: the tests**, then the walkthroughs below.

```
dotnet test
```

Working looks like `Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5`. The tests do
not need the service or Docker running.

## Try it

The commands are for a bash shell: Git Bash on Windows (Windows PowerShell's `curl` is a different
command), or any terminal on macOS and Linux. Responses below are real ones from this build; ids and
timestamps will differ, and response headers other than the status line and `Location` are trimmed.

### 1. A clean MA application reaches `Activated`

```bash
R=$(curl -s -X POST http://localhost:5287/applications -H "Content-Type: application/json" \
  -d '{"market":"MA","identity":{"type":"NationalIdentifier","value":"1234567890123"}}'); echo "$R"
ID=$(echo "$R" | sed -E 's/.*"id":"([^"]+)".*/\1/')
curl -si -X POST http://localhost:5287/applications/$ID/submit; echo
curl -s http://localhost:5287/applications/$ID; echo
sleep 2; curl -s http://localhost:5287/applications/$ID; echo
```

```
{"id":"bcd7dcac-6d72-4f8e-9f9a-680c3e580816","state":"Draft"}
HTTP/1.1 202 Accepted
Location: /applications/bcd7dcac-6d72-4f8e-9f9a-680c3e580816

{"id":"bcd7dcac-6d72-4f8e-9f9a-680c3e580816","state":"Submitted"}
{"id":"bcd7dcac-6d72-4f8e-9f9a-680c3e580816","market":"MA","state":"Submitted","createdAt":"2026-10-04T21:21:59.5254725+00:00","submittedAt":"2026-10-04T21:21:59.6360872+00:00"}
{"id":"bcd7dcac-6d72-4f8e-9f9a-680c3e580816","market":"MA","state":"Activated","createdAt":"2026-10-04T21:21:59.5254725+00:00","submittedAt":"2026-10-04T21:21:59.6360872+00:00"}
```

Submit answers `202 Accepted` with no verdict. Screening runs afterwards, so the first poll still shows
`Submitted` and a later one shows the outcome.

### 2. A flagged application reaches `PendingManualReview` and stays there

An identifier starting with `9` makes the screening stub return a possible sanctions match.

```bash
R=$(curl -s -X POST http://localhost:5287/applications -H "Content-Type: application/json" \
  -d '{"market":"MA","identity":{"type":"NationalIdentifier","value":"9234567890123"}}'); echo "$R"
ID=$(echo "$R" | sed -E 's/.*"id":"([^"]+)".*/\1/')
curl -si -X POST http://localhost:5287/applications/$ID/submit; echo
sleep 2; curl -s http://localhost:5287/applications/$ID; echo
sleep 5; curl -s http://localhost:5287/applications/$ID; echo
```

```
{"id":"5210c802-075d-41d1-8be9-0f1793cf7787","state":"Draft"}
HTTP/1.1 202 Accepted
Location: /applications/5210c802-075d-41d1-8be9-0f1793cf7787

{"id":"5210c802-075d-41d1-8be9-0f1793cf7787","state":"Submitted"}
{"id":"5210c802-075d-41d1-8be9-0f1793cf7787","market":"MA","state":"PendingManualReview","createdAt":"2026-10-04T21:22:01.8266866+00:00","submittedAt":"2026-10-04T21:22:01.9115353+00:00"}
{"id":"5210c802-075d-41d1-8be9-0f1793cf7787","market":"MA","state":"PendingManualReview","createdAt":"2026-10-04T21:22:01.8266866+00:00","submittedAt":"2026-10-04T21:22:01.9115353+00:00"}
```

The submit response is the same `202` as for the clean application. The application then stays in
`PendingManualReview` for good. **No endpoint exists to move it on, and that is deliberate:** a possible
sanctions match must be decided by a compliance officer, and automated code cannot leave this state.

### 3. A clean MD application reaches `PendingBranchVisit`, not `Activated`

MD requires the applicant to sign in person at a branch before the account can be activated.

```bash
R=$(curl -s -X POST http://localhost:5287/applications -H "Content-Type: application/json" \
  -d '{"market":"MD","identity":{"type":"NationalIdentifier","value":"1234567890"}}'); echo "$R"
ID=$(echo "$R" | sed -E 's/.*"id":"([^"]+)".*/\1/')
curl -si -X POST http://localhost:5287/applications/$ID/submit; echo
sleep 2; curl -s http://localhost:5287/applications/$ID; echo
```

```
{"id":"58995643-c568-44dd-8333-fbda7f22ebf3","state":"Draft"}
HTTP/1.1 202 Accepted
Location: /applications/58995643-c568-44dd-8333-fbda7f22ebf3

{"id":"58995643-c568-44dd-8333-fbda7f22ebf3","state":"Submitted"}
{"id":"58995643-c568-44dd-8333-fbda7f22ebf3","market":"MD","state":"PendingBranchVisit","createdAt":"2026-10-04T21:22:09.1245772+00:00","submittedAt":"2026-10-04T21:22:09.2106582+00:00"}
```

Always create applications with an identity, as above: see the last point under What is not built.

## What is not built

- In-memory store. The SQL Server instance in `platform/` is running and unused. Restarting the service
  loses every application.
- No RabbitMQ. Verification runs in a hosted service inside the same process.
- No real World-Check: the screening stub is deterministic, and an identifier starting with `9` returns a
  possible match. Identity document verification (IDNow) is not represented at all.
- No authentication or authorisation.
- Three markets of six: MA, MD, MF. Creating an application for MB, MC or ME returns 400.
- Market comes from the request body rather than from deployment configuration. See `REASONING.md` §4 —
  this is a local shortcut, not the design.
- The draft resource exists, which is the point argued in `REASONING.md` §6, but the endpoints to amend
  it do not. Identity must be supplied at creation: a draft created without one fails validation on
  submit and cannot be fixed. The lifecycle is modelled; incremental editing is not built.

## No transaction boundary

Submit changes the application's state and nothing else. No event is written. The intended design writes
the state change and an outbox row in one transaction, so the event that starts verification cannot be
lost. This does not do that.

## One line changed in `platform/docker-compose.yml`

`SEQ_FIRSTRUN_NOAUTHENTICATION: "True"` was added to the provided compose file so Seq starts without a
first-run password prompt. Current `datalust/seq:latest` images refuse to start without either an admin
password or this opt-out, so this removes a manual setup step. Seq then has no login, as
`platform/README.md` already describes.

It is also exactly what compliance §2 forbids in anything real: access to personal data that is neither
restricted nor attributable to a person. That is part of why identity values are redacted before they
can reach a log.

## How this was built

The design is mine and documented in `REASONING.md`. The code was written with Claude Code against that
design, and I reviewed and committed each piece. I can talk through the files in the repo.
