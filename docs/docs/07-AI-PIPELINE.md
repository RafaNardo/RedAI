# 07 — AI Pipeline

## Context chain

Every stage consumes the **approved** output of the preceding stage.

```text
Evidence
  -> BrandAnalyzer
  -> Approved BrandDNA
  -> CampaignStrategist
  -> Approved Strategy
  -> IdeaGenerator
  -> 12 selected ideas
  -> ContentGenerator
  -> Copy Review Loop
  -> 12 approved copies
  -> CreativeDirector
  -> VisualGenerator + Renderer
  -> Creative Review Loop
  -> Final Campaign
```

## Domain services

- `BrandAnalyzer`
- `CampaignStrategist`
- `IdeaGenerator`
- `ContentGenerator`
- `ContentRevisionAgent`
- `CreativeDirector`
- `VisualGenerator`
- `CreativeRevisionAgent`

All use `IAIClient`.

## Input processing

### Website
V1 only fetches the supplied page and extracts:
- title
- meta description
- OpenGraph metadata
- H1/H2
- readable body text
- representative image URLs when trivial

No site crawler for MVP.

### Images/screenshots/logo
Send as visual evidence to multimodal model.

### PDF
Extract text; render pages to images only where visual context is useful. Do not build OCR unless needed.

## Structured outputs

Every domain step returns a schema from `/contracts`.

Never parse prose to make program decisions.

## Logging

Every real AI call creates `AIRun` with sanitized inputs, schema output, model, duration/status and errors.

## Model configuration

Do not hardcode names in services. Example:

```json
{
  "AI": {
    "Mode": "mock",
    "Models": {
      "Reasoning": "configured-at-runtime",
      "Fast": "configured-at-runtime",
      "Image": "configured-at-runtime"
    },
    "GenerationConcurrency": 3
  }
}
```

Use current models supported by the official OpenAI API when implementing.

## Image policy for RED AI

AI-generated visuals must not contain final typography or logos. Prompt includes `Do not include any text or logos.`

Renderer owns:
- exact text
- logo
- brand colors
- spacing
- CTA
- graphic treatment

## Revision policy

Revision agents return executable plans with minimum required changes.

Possible visual actions:
- CHANGE_COPY
- CHANGE_LAYOUT
- REGENERATE_IMAGE
- CHANGE_COLORS
- CHANGE_TYPOGRAPHY
- CHANGE_ASSET
- NO_CHANGE

Possible copy actions:
- REWRITE_HEADLINE
- REWRITE_SUPPORTING
- REWRITE_CAPTION
- REWRITE_CTA
- ADJUST_TONE
- NO_CHANGE
