# 04 — Design System

## Direção

Dark editorial + creative agency + premium AI tool. Inspirado na linguagem visual das referências Redzone, sem copiar a marca.

## Tokens

```ts
export const tokens = {
  color: {
    bg: '#090909',
    surface: '#111111',
    surfaceElevated: '#181818',
    primary: '#FF3D1F',
    primaryBright: '#FF542D',
    accentSoft: '#FF7654',
    text: '#F6F6F3',
    textMuted: '#929292',
    border: '#272727',
    success: '#68C268',
    warning: '#FFB84D',
    danger: '#E95858'
  },
  radius: { sm: 8, md: 12, lg: 18 }
}
```

## Tipografia

- UI: Inter
- Display: Manrope
- Headlines: weight 700, letter-spacing ~ -0.04em

## Componentes mínimos

- WizardHeader
- WizardProgress
- ProjectCard
- SourceUploader
- SourcePreview
- AnalysisProgress
- BrandPalette
- BrandTraitChips
- ConfidenceBadge
- StrategyMix
- ContentIdeaCard
- SelectionCounter
- ContentDraftCard
- AIRevisionInput
- GenerationProgress
- CreativeGrid
- CreativeReviewPanel
- CreativeVersionStrip
- ResultCard

## Assinatura visual de IA

Usar `✦` nas ações realmente generativas:
- ✦ Mapear identidade
- ✦ Planejar campanha
- ✦ Gerar ideias
- ✦ Pedir alteração
- ✦ Gerar artes
- ✦ Aplicar alteração

## Motion

Somente transições curtas de 180–300ms. Progresso, troca de versão, hover e entrada de cards. Evitar partículas e efeitos gratuitos.

## Layout

Desktop-first para demo. Target recomendado: 1440px. Deve continuar utilizável em notebook 1280px.
