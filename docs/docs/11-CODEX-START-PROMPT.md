# 11 — Prompt to Start Codex Orchestration

Paste the text below after the package is added to the repository.

```text
Read AGENTS.md and every document under /docs, /contracts and /prompts.

You are the Orchestrator for the RED AI MVP.

The product scope was intentionally simplified from a full agency SaaS to a wizard that validates the core campaign engine. Do not restore removed dashboard/user/Meta/publishing features.

First execute Wave 1 from docs/10-MULTIAGENT-PLAN.md.

Before spawning feature agents:
1. inspect the repository
2. bootstrap/fix the documented structure
3. freeze domain entities, DTOs and routes
4. configure PostgreSQL/migrations
5. configure IAssetStorage
6. configure AI_MODE mock/openai
7. configure jobs
8. configure frontend types/API layer
9. configure design tokens
10. make frontend/API/database boot successfully

Write a short CONTRACT-FREEZE.md report with the final shared contracts.

Then run Wave 2 with Agents A, B and C in parallel only where contracts do not conflict.

Integrate after every wave and run build/tests.

Then execute Wave 3 and hand the integrated branch to Agent D for demo QA.

Do not ask me for decisions already specified in the documentation. For implementation ambiguity, choose the simplest reliable approach that preserves the sacred flow.

Definition of done is not compilation. Definition of done is a clean, presentation-ready end-to-end wizard that can run in mock mode and optionally use OpenAI mode.
```
