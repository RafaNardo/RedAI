# 02 — UX do Wizard

Use `reference/mockups/02-red-ai-wizard-full-flow.png` como referência principal.

## Linguagem de navegação

O wizard deve parecer uma experiência editorial contínua. Evitar sidebar e dashboard tradicional.

Topbar mínima:

`RED AI                                      Projetos`

Indicador de progresso discreto:

`01 Marca — 02 Brand DNA — 03 Estratégia — 04 Ideias — 05 Conteúdo — 06 Artes — 07 Resultado`

## Home

Headline:

> Transforme qualquer marca em uma campanha com IA.

CTA: `+ Nova análise`

Cards de projetos recentes mostram:
- marca
- nome da campanha
- quantidade final
- passo/status atual
- `Continuar ->`

## Passo 1 — Fontes da Marca

Campos:
- Nome da marca (required)
- Instagram / @ (optional, metadata only)
- Website (optional)
- Contexto manual (optional)
- Upload múltiplo: PNG/JPG/WEBP/PDF/SVG de logo

CTA: `✦ Mapear identidade`

## Passo 2 — Análise

Mostrar progresso real do job:
- lendo materiais
- identificando cores/elementos
- entendendo posicionamento/tom
- analisando conteúdo
- construindo Brand DNA

Ao concluir, CTA `Ver identidade`.

## Passo 3 — Brand DNA

Mostrar:
- confiança
- paleta
- personalidade
- tom de voz
- público provável
- produtos/serviços
- linguagem visual
- padrões de conteúdo
- oportunidades
- restrições / coisas a evitar

Usuário pode editar campos/chips e então `Usar este Brand DNA`.

## Passo 4 — Briefing

Campos:
- campanha
- objetivo (`AI decide` como default)
- quantidade final (12 default)
- período
- contexto adicional
- anexos da campanha

CTA: `✦ Planejar campanha`

## Passo 5 — Estratégia

Mostrar:
- objetivo estratégico
- racional
- mix de conteúdo
- pilares
- público prioritário
- direção criativa

Ações:
- Refazer
- Editar
- Aprovar estratégia

## Passo 6 — 30 Ideias

Exatamente 30 cards. Filtros por categoria.

Cada card:
- checkbox
- categoria/pilar
- título
- descrição curta
- formato sugerido
- ângulo

Topo: `X / 12 selecionados`.

Ações:
- `✦ Selecionar melhores 12`
- `Gerar outras ideias`
- `Continuar com 12`

Nunca permitir seguir com != 12.

## Passo 7 — Conteúdo Proposto

Antes de gastar com imagens, revisar texto.

Cada ContentItem mostra:
- headline
- supporting text
- caption
- CTA
- hashtags opcionais
- direção visual

Ações:
- Aprovar
- Editar manualmente
- `✦ Pedir alteração`

Prompt natural deve gerar nova ContentRevision, preservando versões anteriores.

Só permitir `Gerar artes` quando 12 copies estiverem aprovadas.

## Passo 8 — Produção

Mostrar progresso agregado e thumbnails conforme terminam.

Exemplo:
- Copy ✓ 12/12
- Direção criativa ✓ 12/12
- Assets 9/12
- Artes finais 7/12

Cada item falha independentemente e possui Retry.

## Passo 9 — Revisão Visual

Grid com 12 artes e status.

Abrir uma arte mostra:
- arte grande
- versões V1/V2/V3
- campo `O que deseja alterar?`
- quick actions: Mais clean / Mais premium / Outra imagem / Menos texto / Nova headline
- Aprovar esta versão

A revisão gera um RevisionPlan e aplica a mudança mínima.

## Passo 10 — Resultado

Headline: `Campanha pronta.`

Grid 3x4 com os 12 finais.

Para cada post:
- baixar imagem
- copiar legenda

Ação global:
- `Baixar campanha .ZIP`

O ZIP deve conter imagens, `captions.txt` ou JSON/CSV simples com texto de cada conteúdo.
