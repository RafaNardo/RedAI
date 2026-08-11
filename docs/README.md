# RED AI — MVP Wizard Package

Pacote de especificação para construir a POC funcional do RED AI: uma engine AI-first que transforma materiais brutos de uma marca em uma campanha social revisada e pronta para exportação.

## Objetivo do MVP

Validar a engine central, não um SaaS completo.

Fluxo principal:

`Home -> Fontes da Marca -> Brand DNA -> Estratégia -> 30 Ideias -> Seleção de 12 -> Conteúdo -> Revisão de Copy -> Geração Visual -> Revisão Visual -> Resultado`

## O que foi removido do MVP

- autenticação
- usuários/workspaces
- dashboard/admin portal complexo
- Meta API / Instagram OAuth
- publicação automática
- billing
- analytics
- RBAC
- colaboração em tempo real
- vídeo/reels

## Como usar este pacote

1. Leia `AGENTS.md`.
2. Leia `docs/01-PRODUCT.md`, `02-UX-WIZARD.md` e `03-ARCHITECTURE.md`.
3. Use `docs/10-MULTIAGENT-PLAN.md` para iniciar o Codex em loop multiagente.
4. Use `prompts/` como fonte de verdade dos prompts de IA.
5. Use `contracts/` como contratos canônicos do backend.
6. Use `reference/mockups/02-red-ai-wizard-full-flow.png` como principal referência visual.

## Stack sugerida

- Frontend: Next.js + TypeScript + Tailwind + shadcn/ui
- Backend: ASP.NET Core .NET 10 + EF Core
- Banco: PostgreSQL
- AI: OpenAI Responses API + geração/edição de imagem
- Storage: `IAssetStorage`, Local no dev e S3-compatible depois
- Infra local: Docker Compose

## Regra principal

O produto está pronto quando o fluxo inteiro pode ser executado sem editar banco/código manualmente.

## Referências visuais

- `reference/mockups/01-red-ai-overview.png`
- `reference/mockups/02-red-ai-wizard-full-flow.png`
- `reference/redzone/` contém as imagens usadas como inspiração estética.
