# Atlas Onboarding

## Changes to the provided `platform/`

One line changed in the provided `docker-compose.yml` (`SEQ_FIRSTRUN_NOAUTHENTICATION: "True"`) so Seq
starts without a first-run password prompt. Current `datalust/seq:latest` images refuse to start without
either an admin password or this opt-out; with it, Seq comes up with no login, as `platform/README.md`
already describes. That removes a manual setup step.

It is also exactly what §2 of the compliance notice forbids in anything real: access that is neither
restricted nor attributable to a person. That is part of why identity document values are redacted before
they can reach a log.
