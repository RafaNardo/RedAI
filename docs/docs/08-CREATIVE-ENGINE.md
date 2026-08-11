# 08 — Creative Engine

## Goal

Create repeatable agency-quality social posts while preserving deterministic brand typography/text.

## Pipeline

```text
Approved ContentRevision
  -> CreativeDirector
  -> CreativeBrief
  -> LayoutSelector
  -> VisualGenerator (optional)
  -> CreativeLayout JSON
  -> HTML/CSS Renderer
  -> PNG 1080x1350
  -> CreativeVersion
```

## Templates V1

1. `editorial-bold` — oversized headline, strong negative space
2. `minimal-center` — central statement, low density
3. `split-image` — image + editorial copy split
4. `statement` — one high-impact phrase
5. `educational` — title/support + simple info hierarchy
6. `promotional` — offer/value + CTA

No drag-and-drop editor.

## CreativeBrief minimum fields

- purpose
- content format
- preferred template
- image required boolean
- image direction
- composition
- mood
- palette recommendation
- hierarchy
- elements to avoid

## CreativeLayout

Use schema in `contracts/creative-layout.schema.json`.

## Image generation prompt rule

Always request commercial background/visual only and reserve negative space for text.

## Versioning

Never overwrite. Revision always creates V+1. `is_selected` marks the user's final choice.

## Revision algorithm

1. Load selected/current CreativeVersion.
2. Send version metadata + user instruction to CreativeRevisionAgent.
3. Validate RevisionPlan.
4. Execute minimum operations.
5. Re-render.
6. Save new CreativeVersion.

Examples:

`Troca só a headline` -> CHANGE_COPY, no image generation.

`Use uma família no lugar dessa pessoa` -> REGENERATE_IMAGE, preserve typography/layout unless incompatible.

`Mais clean` -> CHANGE_LAYOUT + possibly CHANGE_COLORS, no image regeneration by default.
