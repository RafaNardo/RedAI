# 09 — Demo Setup / Runbook

## Required modes

### Mock (default)
- no OpenAI key required
- deterministic Brand DNA/strategy/ideas/content
- local demo visual assets
- full wizard works

### OpenAI
- real analysis/generation
- key in environment only
- fallback to mock assets for visual failures must be possible in Development/Demo

## Suggested .env

See `config/.env.example`.

## Boot target

```bash
docker compose up --build
```

Expected:
- web on configured frontend port
- api on configured API port
- PostgreSQL
- storage directory volume

## Demo seed

Seed two projects:

1. Redzone MKT — completed/near completed, establishes credibility on Home.
2. Cassel Seguros — project that can be reset and run through the wizard.

## Demo sequence (5–8 min)

1. Open Home and briefly show recent projects.
2. Click `Nova análise`.
3. Use Cassel or Redzone reference assets.
4. Show Brand DNA extraction.
5. Approve it.
6. Create campaign briefing.
7. Show strategy.
8. Generate 30 ideas and click `Selecionar melhores 12`.
9. Swap one idea manually to prove human curation.
10. Generate copy.
11. Ask one content: `deixe menos comercial e mais direto`.
12. Approve copies.
13. Generate creatives.
14. Open one creative and ask `deixa mais clean e a headline menor`.
15. Compare V1/V2.
16. Approve final.
17. Show 12-result grid.
18. Export ZIP.

## Presentation fallback

If OpenAI or network fails:
- switch to AI_MODE=mock
- demo reset
- repeat the exact flow

Do not depend on live generation to prove UI/flow.

## Final pre-demo checklist

- clean checkout boot tested
- migrations run
- seed works
- mock path tested twice
- OpenAI path tested at least once
- no secrets committed
- 30 ideas count verified
- 12 selection validation verified
- copy revision creates new revision
- creative revision creates new version
- export opens successfully
- browser zoom 100%
- no console errors in happy path
