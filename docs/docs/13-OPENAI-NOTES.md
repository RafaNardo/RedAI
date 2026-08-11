# 13 — OpenAI Implementation Notes

Use the current official OpenAI API documentation when wiring the implementation rather than relying on model names captured in this package.

Architecture assumptions for this MVP:

- Use the Responses API for new text/multimodal model interactions.
- Use machine-readable structured outputs/schema validation for Brand DNA, Strategy, Ideas, Content, CreativeBrief and RevisionPlan.
- Use image generation/edit capabilities only for visual assets/backgrounds; exact post typography remains deterministic in our renderer.
- Put all model IDs in environment/configuration.
- Log AI operations in `ai_runs`.
- Keep `AI_MODE=mock` independent from live API availability.

Do not use legacy Assistants concepts as the core of this implementation.
