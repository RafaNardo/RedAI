# RED AI — Codex Master Instructions

You are the lead engineer responsible for building the RED AI MVP.

RED AI is an AI-first social media campaign engine for agencies. This MVP intentionally validates the engine rather than a full SaaS shell.

## Sacred flow

Home
-> New Analysis
-> Brand Sources
-> Analyze Brand
-> Review + Approve Brand DNA
-> Campaign Brief
-> Generate + Approve Strategy
-> Generate exactly 30 Ideas
-> Human selects exactly 12
-> Generate 12 Content Drafts
-> Review copy iteratively until approved
-> Generate 12 Creatives
-> Review creatives iteratively with version history
-> Final Result
-> Export campaign ZIP

Never prioritize secondary work over this path.

## MVP constraints

DO NOT implement:
- authentication
- users/workspaces
- Meta API
- Instagram OAuth
- scraping Instagram
- automatic social publishing
- analytics
- billing/subscriptions
- RBAC
- video generation
- Canva integration
- real-time collaboration
- microservices
- RabbitMQ/Kafka
- Kubernetes

The Instagram handle is metadata only. Brand intelligence uses website content, screenshots, logos, uploaded images, PDFs and manual context.

## Architecture

Modular monolith.

Suggested backend projects:
- RedAI.Api
- RedAI.Application
- RedAI.Domain
- RedAI.Infrastructure

Frontend:
- apps/web

Core domain modules:
- Projects
- Brand Intelligence
- Campaign Intelligence
- Content Intelligence
- Creative Engine
- Jobs
- Storage
- AI

Do not add abstractions without a concrete MVP use case.

## AI rules

Use a shared `IAIClient` and domain services:
- BrandAnalyzer
- CampaignStrategist
- IdeaGenerator
- ContentGenerator
- ContentRevisionAgent
- CreativeDirector
- VisualGenerator
- CreativeRevisionAgent

All machine-readable outputs must use structured outputs / schema validation.

Support:
- `AI_MODE=mock`
- `AI_MODE=openai`

The full app must work in mock mode.

Do not hardcode a model name deep in services. Model selection is configuration.

## Creative rules

AI image generation is for visual assets/backgrounds, not final exact typography.

Final artwork is composed by the renderer so headline, CTA, logo and brand colors are deterministic.

Start with six templates:
- editorial-bold
- minimal-center
- split-image
- statement
- educational
- promotional

Every creative change creates a new `CreativeVersion`. Never overwrite history.

## Review loops

Copy review:
ContentDraft V1 -> user instruction -> ContentRevision V2 -> ... -> approved

Visual review:
Creative V1 -> user instruction -> RevisionPlan -> Creative V2 -> ... -> selected/approved

Apply minimum-change behavior. If the user asks only to change copy, do not regenerate imagery.

## Async jobs

Long operations return `202 Accepted` with a job ID. Frontend polls jobs.

Global job states:
- queued
- running
- completed
- completed_with_errors
- failed

Generation of 12 items must be independently fault-tolerant. 1 failure must not destroy 11 successes.

## Design direction

Premium dark editorial creative-agency UI.

Tokens:
- background: #090909
- surface: #111111
- surface-elevated: #181818
- primary: #FF3D1F
- primary-bright: #FF542D
- text: #F6F6F3
- muted: #929292
- border: #272727

Typography:
- Inter: UI
- Manrope: display

The wizard should feel like an interactive landing page, not an admin dashboard.

## Quality gate

Before marking a wave done:
1. compile/build
2. tests
3. lint
4. run from clean setup
5. manually verify affected wizard path
6. fix obvious UI issues

No TODO placeholders in the happy path.

## Priority

1. demo stability
2. output quality
3. UX/polish
4. AI integration
5. code elegance
6. secondary improvements

The project is done when a new user can complete the entire sacred flow without touching code or database manually.
