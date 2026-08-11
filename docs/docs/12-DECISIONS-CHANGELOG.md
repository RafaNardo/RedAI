# 12 — Decisions / Changelog

## MVP simplification

The original concept included a SaaS-style dashboard, users/workspaces, client management, approval portal, calendar and future publishing.

For the validation MVP, those were removed from the critical path.

### New product shell

- simple Home with recent analyses
- one Project per wizard run
- resumable current step
- single campaign per project in the MVP UX
- no login

### Brand acquisition

Meta/Instagram API removed entirely for MVP.

Instagram `@` is reference metadata. Analysis comes from:
- screenshots
- logo
- website
- images/PDFs
- manual context

### Review split

Copy review now occurs **before** visual generation to reduce cost and improve quality.

Separate histories:
- ContentRevision
- CreativeVersion

### Architecture preserved for growth

Backend remains modular and API-first so a future Agency SaaS UI can be added without rewriting Brand/Campaign/Content/Creative modules.

### Storage

Storage abstraction remains from day one. Local in development, S3-compatible later.

### Demo safety

`AI_MODE=mock` is mandatory. Live OpenAI is optional for the demo, not a dependency on presentation stability.
