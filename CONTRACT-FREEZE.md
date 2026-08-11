# RED AI — Contract Freeze (Wave 1)

Date: 2026-08-10

## Structure

- `apps/web`: Next.js Wizard client.
- `apps/api/src/RedAI.Domain`: aggregate and invariant definitions.
- `apps/api/src/RedAI.Application`: use cases, AI and storage interfaces.
- `apps/api/src/RedAI.Infrastructure`: EF Core/PostgreSQL, local storage and background jobs.
- `apps/api/src/RedAI.Api`: ASP.NET Core HTTP boundary.

The physical `apps/api` location is an approved deviation from the original suggested `src/` root; the modular boundaries are unchanged.

## Canonical rules

- A project owns one campaign in the MVP flow.
- Machine-readable AI output is validated against the JSON schemas in `docs/contracts`.
- Ideas are always generated as 30; selection requests with any count other than 12 return HTTP 400.
- Copy and creative changes append versions; no version is overwritten.
- Long generation endpoints return `202 Accepted` and a job resource.
- `AI_MODE=mock` is deterministic and has no external dependency. `AI_MODE=openai` is server-side only and reads `ai-api-key` from .NET configuration/user-secrets.
- Assets are addressed by storage key. Local storage is the development implementation.

## HTTP contracts

The route map and request shapes in `docs/docs/06-API.md` are frozen as the public API. Job responses use `id`, `status`, `progress`, `completedSteps`, `totalSteps`, and `message`.

## Runtime

Local containers use `podman compose -f compose.yml up --build`. PostgreSQL is required by the API; the web app talks to `/api` through `NEXT_PUBLIC_API_BASE_URL`.
