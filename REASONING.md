# Reasoning

The requirements contradict each other in ways that change what can be built. This is
what I found, what I decided, and where I drew the line.

Where a section argues for something I did not build, it says so. `README.md` lists what
runs; this document says what it should be.

**The headline.** `ATLAS-1` asks for a synchronous POST returning a final verdict in
under three minutes, and puts "anything involving a human reviewing anything" out of
scope. Compliance GC-2026-0814 §3 requires that a possible sanctions or PEP match goes to
a human, allows it up to 48 hours, and says it may under no circumstances be automated or
bypassed. The ticket excludes by name the thing the compliance position requires. It was
raised on 19 August and closed with "let's discuss offline"; nothing follows. So I did not
build the API as specified, and most of what follows comes from that.

One other thing shaped how I read this. Sam's last message before three weeks of leave,
26 August: *"ping me before you commit to the data model, there are a few things in here
that look simple and aren't."* The person who understood the modelling problems left and
the kickoff happened without him. I treated the data model as the centre of the task.

The process as designed: [`docs/atlas-onboarding-bpmn.svg`](docs/atlas-onboarding-bpmn.svg).
It shows the remote activation path; the MD branch is left off to keep it readable. Its
failed-validation path ends at `Rejected`, which the code deliberately does not do — see
Other calls. It also shows an identity document check that the code does not implement.

---

## What I decided Atlas is

An application is a resource with a lifecycle, not a request with a verdict. One of its
states can only be left by a person. Approval and activation are separate events.
Identity is presented, not stored as a field. Markets differ in configuration, never in
code. Nothing that calls a provider runs inside a request.

The rest of this document is why.

---

## 1. The API cannot return a verdict

A 201 carrying `APPROVED` or `REJECTED` needs the verdict to exist when the response is
written. Where a review is pending, it does not.

Even without the compliance rule it would not work. Ingress and load balancer timeouts
kill a held request in minutes. A held request holds a connection and a thread, so enough
of them and the clean applications start failing too. With three replicas the request is
not pinned to the process holding the state, so a deploy or a pod eviction loses
everything in flight.

**Built.** `POST /applications/{id}/submit` returns 202 with a status location.
`GET /applications/{id}` returns current state. Verification runs outside the request.

`PendingManualReview` has no automated exit, enforced by construction:

- `Application.State` has an `internal` setter, so nothing outside `Atlas.Domain` can
  assign it.
- The automated transition table **has no row** for `PendingManualReview`.
  `TryApplyAutomated` refuses anything absent from the table. There is no
  `if (state == PendingManualReview)` guard for somebody to delete later.
- `ApplyComplianceDecision` is the only way out, and it requires an officer identity,
  which is recorded. §2 requires access to be attributable, so the signature enforces it.

**What that does not prove.** `ApplyComplianceDecision` is public, so automated code could
call it with an invented officer id. The types make bypassing the review a deliberate,
named, attributed act; they cannot show a human did it. Only an authenticated endpoint
requiring a compliance role can, and there is none here. Persistence is a second way in —
EF Core can write an internal setter by reflection and a raw `UPDATE` ignores the type
system, so the guard at that layer is a database constraint.

The three minutes still holds for a clean screening result, which resolves in seconds.
What I removed is the guarantee for the ones that cannot lawfully resolve in three.

---

## 2. The national identifier is not a field

Three problems sit in one field. Nora's *"can we just validate per market?"* addresses the
first.

**Format varies.** A validation rule per market handles it.

**The value may not exist.** Sam, 29 July: MF has many residents with no national
identifier at all, mostly refugees, who onboard on a passport instead. So "required" is
not a market-specific rule, it is a different model: the customer presents a national
identifier *or* a passport. Sam said this and it went unanswered.

**It cannot be the key.** Annex B note 2 forbids it, and it would be a poor key anyway: it
can be reissued, it is absent for some customers, and it is personal data subject to
erasure. That last point decides it — **you cannot erase a primary key and still hold the
record AML requires you to keep for ten years.** Using the identifier as the key makes §4
and §5 impossible to reconcile. A surrogate makes them difficult but possible.

**Built.** `Application.Id` is a server-generated `Guid`. An `IdentityDocument` value
object carries a type discriminator, and its `ToString()` is redacted so the value cannot
reach a log by accident.

Validation is an `IIdentifierRule` interface with one implementation per rule, and market
configuration maps a market code to its implementation. Adding a market is a configuration
entry, not another branch in a switch. This is the part that satisfies "market differences
are configuration" — a switch statement is code; an interface selected by configuration is
configuration.

---

## 3. MD cannot be activated remotely

Annex B: the applicant must attend a branch in person, with a wet signature, before
activation.

The channel treats this as temporary. Nora: *"We'll get an exemption."* Petra, 22 July,
had already asked the regulator twice and been told no: *"We've been asking for the
exemption for two years."* Annex B note 1 confirms it is outstanding since 2024 and **"no
relief should be assumed."**

If approval and activation are one event, MD is a fork. If they are separate states, MD is
just a different activation policy selected by configuration.

**Built.** `Approved` and `Activated` as distinct states. Market configuration carries an
`ActivationPolicy` of `Remote` or `InPersonWithWetSignature`. `PendingBranchVisit` exists
for all six markets and is simply never entered where the policy is remote.

The cost of supporting MD this way is one enum value and one state. The cost of not
separating them is a rewrite the first time a second market needs it.

**Not built.** There is no branch system behind `PendingBranchVisit`, and nothing moves an
application out of it.

---

## 4. One codebase is not one deployment

Compliance §1: personal data, identity documents and biometric data must not leave the
customer's country of residence.

Teo, 18 July: *"everything runs in West Europe. One region, one AKS cluster, one SQL
instance… Don't overthink it."* He is not wrong — that predates the compliance position by
three weeks and describes the estate as it was, not an answer to a requirement that did
not yet exist.

The ticket asks for "one codebase, six countries", and the complaint behind it is the card
portal getting forked six ways. A single artifact deployed six times with different
configuration satisfies that. One codebase does not mean one deployment, and it does not
mean one database.

**What that means for where the market comes from.** In production the deployment is the
market: the MD instance only ever serves MD, so there is no resolution step at all. The
customer picks their country of residence in the app and that selects the endpoint. A
market field in the request body is the wrong answer for anything real, because it lets
the caller choose which jurisdiction their data lands in.

**Not built.** The code takes the market from the request body, because there is one
instance and nothing to route between. That is a local shortcut and I would not ship it.
Replacing it means reading the market from deployment configuration and ignoring the
field, which is a small change and a large topology.

**Unresolved.** Market is declared residence at submission and verified residence later.
If someone declares MD and their document shows MA, the data is already in the wrong
country. There is no fix inside this service.

---

## 5. Access attribution covers systems, not just people

§2 requires every access to a customer record to be attributable to "the person **or
system**" that made it, and says shared credentials are not acceptable.

"Or system" is the part that is easy to miss. A service reading customer records needs its
own identity, not a connection string shared across the estate.

The second half is the distinction between telemetry and a record. An access log retained
ten years is not Seq — Seq is operational logging, and the provided instance has no
authentication on its UI. A compliance access log is evidence: append-only, queryable,
retained alongside the record. Those are separate stores with separate lifecycles.

**Built.** `ApplyComplianceDecision` records `ReviewedBy` and `ReviewedAt`, so a manual
decision is attributable to a named officer.

**Not built.** There is no access log. `ReviewedBy` records who made one decision, not
every read of a customer record, which is what §2 actually requires. Per-service managed
identities are a deployment concern and there is nothing to deploy to here.

---

## 6. The single POST carries neither the payload nor the lifecycle

Sam, 29 July: *"the base64 images in the POST body are going to be several MB each."*
Nora: *"Let's park the image thing."* It was never unparked. base64 inflates binary by
about a third, so a passport scan and a selfie is comfortably over 10 MB of JSON, and the
whole body must be buffered and deserialised before a single field can be validated.
Kestrel's default limit is 30 MB and ingress limits are often lower. Across three replicas
that is a memory problem, not a payload size.

Ivana, 6 August, asked what happens when someone's train goes into a tunnel halfway
through. Nora: *"Save and resume, it's table stakes."* Ivana pointed out the API in the
ticket cannot do that. Nora: *"Mobile can hold the state locally until it's complete?"*

Save and resume needs a server-side resource that exists before submission, and a single
POST has none. The workaround puts identity documents and biometric data on a handset for
an unbounded period, where access is neither restricted nor attributable, and §2 requires
both.

**Built.** The application is a resource created before submission. Submit takes no body
at all, so no bytes cross it.

**Not built.** There are no document endpoints. `Application` carries a
`DocumentReferences` list that nothing populates, and blob storage is not wired up.
Identity must be supplied at creation, because there is no endpoint to amend a draft. The
lifecycle is modelled; the editing surface is not.

---

## 7. "Build in retries" is not the whole answer

Retries alone make it worse — they multiply load on a provider that is already struggling,
and every client retries at the same moment. A timeout comes first, because an unbounded
wait on a hung provider holds a connection until the pool is empty. Then backoff with
jitter. Then a circuit breaker.

And idempotency, which is the one that gets missed. A timeout does not tell you the call
failed, it tells you that you did not get an answer. Retrying a screening call that already
created a case on the provider's side is a correctness problem.

**Not built.** The provider is an in-process stub, so there is nothing to be resilient
against and no package here to do it with. In production this is Polly composed with
`HttpClientFactory`.

The larger point is downstream of §1. Because verification runs outside the request, a
provider outage leaves the application visibly in `Verifying` rather than returning a 500.
Nothing picks it up again yet — that recovery belongs with the message boundary, and
until then a failed screening leaves the application stuck rather than delayed.

---

## 8. Retention against erasure

§4 requires ten-year retention; §5 requires erasure on request. Both are real.

The resolution is to split the record: retain the AML-mandated set, erase everything
outside it. Which fields constitute that set is a legal determination, not an engineering
one, and the annex does not give it.

**Not built.** No field is marked as retained or erasable. The one decision this did force
is in §2 — the surrogate key, because erasing a primary key is not possible while the
record has to survive. What I avoided was doing nothing and hoping neither obligation gets
tested.

---

## What I built, and the line I drew

One flow, end to end, including the awkward parts: create an application, submit it,
screen it, and either approve it or hand it to a person. Five tests cover the parts most
likely to break.

That slice is where every conflict above actually bites. The manual review path is the
thing the ticket gets wrong, so it is the thing worth getting right.

Three markets have real rules, chosen so each breaks something different. **MA** is the
ordinary case. **MF** is where the national identifier may not exist. **MD** is where
approval does not activate. MB, MC and ME return 400 and are marked as unconfigured.

**On service boundaries.** The test I use is whether two things can be deployed and
changed independently. An API and its background worker cannot, because they share a
schema and a migration history, so they are one service split across two projects.

`Atlas.Verification` exists as a project and does nothing. Everything runs in
`Atlas.Onboarding` today. The intended split is that Verification owns provider
integration and the provider credentials, because those change on the provider's schedule
and belong in one place.

That split is not simply moving `VerificationProcessor` across, and it is worth saying why
because it fails my own test. The processor loads and saves the whole `Application`
through `IApplicationStore`. Moved as it is, Verification would read and write
Onboarding's table and the two would share a schema. The diagram already shows the shape
that works: Onboarding publishes `VerificationRequested`, Verification calls the providers
and publishes `VerificationCompleted`, and **Onboarding applies the state transition**. So
the processor splits — the provider call goes to Verification, the state machine stays
with the data it owns.

A production Atlas would go further: documents separately, because blob storage has a
different cost and scaling profile; the compliance review queue separately, because it has
different users and must stay available when onboarding is not. I drew fewer boundaries
than that for two reasons. Two services I can make correct show more than five I can
sketch. And §1 makes every boundary expensive, because any service holding personal data
deploys per market, so six services means six deployments each.

---

## Other calls, and what I rejected

**The store interface is async with an explicit `Save`,** although the in-memory
implementation needs neither. With a dictionary you mutate the object and it is saved;
with EF Core you need an explicit save inside a transaction. Writing the synchronous
version first would mean changing every call site later. The in-memory store still
forgives a forgotten `Save`, because it hands back the same object reference, so that is a
bug the SQL swap would surface rather than prevent.

**`FindByState` is on the store rather than a queue.** The verification trigger needs to
find submitted applications, and the same query with a different argument is what a
compliance review queue needs. Rejected: firing verification from the submit handler,
which couples the request to the work. What breaks under SQL: with more than one worker a
bare query lets two of them claim the same row, which needs an atomic claim.

**Verification logic is in a plain class, not the hosted service.** The hosted service
finds work and calls it, in 29 lines. See the boundary note above for how that class
splits when Verification becomes real.

**Activation is an enum, not a market check.** Rejected: `if (market == MD)`, which treats
a market code as a capability. Also rejected: a boolean, which is the right shape but does
not survive a third mode.

**A failed market validation returns 400 and leaves the application in `Draft`.** The
diagram shows it ending at `Rejected`. The code does not, because a rejected application
is a record AML requires us to keep for ten years, and creating one because somebody
mistyped a digit makes no sense.

**The screening stub is deterministic.** An identifier beginning with `9` returns a
possible match. Rejected: anything random, because the manual review path is the most
important thing here and a reviewer has to be able to trigger it on purpose.

**Three interfaces, no more:** `IIdentifierRule`, `IApplicationStore`,
`IScreeningProvider`. Rejected: abstracting the market resolver, the audit sink and the
notification path, all of which I could justify and none of which I know enough about yet
to get right.

---

## Open questions

1. Does "must not leave the country of residence" apply to data at rest only, or to
   processing as well? The strict reading drives a six-region topology.
2. Market is declared residence at submission and verified later. What happens when they
   disagree, and how early is residence confirmed?
3. Which fields constitute the AML-mandated set that survives an erasure request?
4. Is MD in scope for launch? The ticket says six markets at once, the channel says MD may
   be phase two, and Annex B says assume no relief.
5. Is three minutes a product guarantee or a target for the clean path?
6. The 48-hour window has no stated expiry. What is the correct state for an application
   nobody reviews in time? Any automated transition is what §3 forbids, so the answer is
   alerting or escalation, and which is a business decision.
7. Who are the compliance officers per market, and how do they authenticate? §2 forbids
   shared credentials for anything that reads a customer record.
8. For an MF resident with neither a national identifier nor a passport, is there a
   fallback?
9. Annex B gives no format for the MF national identifier. I accept any non-blank value.
   What is the real format for MF citizens who hold one?

---

## Known limitations

What runs is in `README.md`. What is missing:

- **No authentication or authorisation.** §2 makes it mandatory before any real use.
- **No access log.** `ReviewedBy` records who decided a manual review, not every read of a
  customer record, which is what §2 requires.
- **Market comes from the request body**, not from deployment configuration. See §4 — this
  is a local shortcut, not the design.
- **In-memory store.** The SQL Server instance in `platform/` runs and is unused.
- **No RabbitMQ**, and no transaction boundary. Submit changes state and nothing else; the
  intended design writes the state change and an outbox row in one transaction so the
  event cannot be lost.
- **`Atlas.Verification` is an empty placeholder.** The screening stub and the processor
  run inside `Atlas.Onboarding`.
- **Screening is stubbed. Identity document verification (IDNow) is not represented at
  all** — no step, no stub. The diagram shows it; the code does not.
- **No timeout, retry or circuit breaker**, because there is no real call to protect. A
  screening failure leaves the application stuck in `Verifying`; nothing retries it.
- **No document endpoints.** `DocumentReferences` exists and nothing populates it. No blob
  storage.
- **No draft editing.** Identity must be supplied at creation.
- **Erasure is not modelled.** No field is marked retained or erasable.
- **Market rules for MA, MD and MF only.** MB, MC and ME return 400.
- **Nothing leaves `PendingBranchVisit`.** There is no branch system.
- One line changed in `platform/docker-compose.yml` so Seq starts without a first-run
  password prompt. It is also what §2 forbids in anything real, which is part of why
  identity values are redacted before they can reach a log.

---

## What I would do next

Not more code. Three of the open questions change the architecture and no amount of
building resolves them: whether residency applies to processing or only storage, which
fields are AML-mandated, and whether MD is in scope for launch. Each could invalidate work
done before the answer arrives.

Then authentication, because §2 makes it mandatory.

Then persistence and the message boundary together, since the outbox only means anything
with a transaction behind it.

Then the compliance review service, which is the boundary I most regret collapsing.
