# 10 — Multi-Agent Implementation Plan

## Team

Use one Orchestrator + four workers.

### Orchestrator
Owns architecture/contracts/integration and is the only agent allowed to change frozen shared contracts without explicit coordination.

### Agent A — Backend + AI
Projects, sources, Brand DNA, campaign strategy, ideas, content generation/revision, jobs, OpenAI/mock.

### Agent B — Creative Engine
CreativeBrief, renderer, image generation adapter, template system, revisions/versioning, export assets.

### Agent C — Frontend Wizard
Home and all wizard pages, polling, loading/error states, visual polish.

### Agent D — QA / Demo
Clean run, seeds, regression, P0/P1 fixes, runbook validation.

## Iteration / Wave 1 — Contract Freeze + Skeleton

Orchestrator:
- bootstrap repo
- Docker Compose
- database connection/migrations base
- domain entities
- canonical DTOs/contracts
- API route map
- shared frontend types or OpenAPI generation
- storage abstraction
- AI_MODE abstraction
- design tokens
- app/API health

Exit criteria:
- frontend boots
- API boots
- migration succeeds
- DB health succeeds
- contract freeze report written

## Wave 2 — Parallel Core

Agent A:
- Projects + sources
- website importer
- brand pipeline
- campaign/strategy/ideas/content
- mock + OpenAI structured output

Agent B:
- six templates
- renderer
- creative layout
- mock visual generator
- version model

Agent C:
- Home
- sources
- analysis progress
- Brand DNA
- campaign
- strategy
- ideas
- content review
- production/review/result shells

Orchestrator integrates only after tests/build pass in each branch.

## Wave 3 — Integration + Demo QA

Agent A:
- real job/progress integration
- copy revision loop

Agent B:
- real visual pipeline + revision plans + export

Agent C:
- wire all endpoints, errors/retry, polish

Agent D:
- full clean setup
- execute sacred flow twice in mock
- once with OpenAI when available
- classify P0/P1/P2

No new features in Wave 3.

## P0

- app does not start
- wizard blocked
- brand profile not persisted
- 30 ideas count wrong
- cannot select exactly 12
- content generation broken
- creative renderer produces no usable output
- revision loops break history
- result/export broken

## P1

- ugly/broken layout
- unclear loading
- one secondary error state
- weak demo seed
- minor copy/status bug

## P2

- extra animations
- mobile perfection
- advanced filtering
- keyboard shortcuts
- rich settings

Ignore P2 until P0/P1 are clear.
