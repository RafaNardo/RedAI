# 01 — Product Definition

## One-line proposition

**RED AI transforma uma marca em uma campanha social completa, revisável e exportável.**

## Hipótese a validar

Uma agência consegue reduzir drasticamente o tempo entre receber materiais de um cliente e chegar a uma campanha coerente de 12 conteúdos prontos, mantendo curadoria humana em pontos-chave.

## O que o MVP valida

1. A IA consegue entender uma marca a partir de fontes imperfeitas.
2. O Brand DNA resultante é útil o suficiente para orientar criação.
3. A estratégia proposta parece coerente para um humano.
4. As 30 ideias possuem variedade e intenção estratégica.
5. Um humano consegue selecionar 12 rapidamente.
6. A geração de copy é boa o suficiente para revisão incremental.
7. A engine visual produz peças consistentes com identidade aprovada.
8. Instruções naturais de revisão alteram somente o necessário.
9. O resultado final é apresentável/exportável.

## Persona inicial

Dono/gestor de agência pequena ou média e social media que precisa produzir muitas campanhas por mês.

## Fluxo principal

1. Home / projetos recentes.
2. Nova análise.
3. Entrada de nome, @ de referência, website, contexto e materiais.
4. Brand Analyzer cria Brand DNA.
5. Usuário revisa e aprova Brand DNA.
6. Usuário fornece briefing da campanha.
7. Campaign Strategist propõe estratégia.
8. Usuário revisa/aprova.
9. Idea Generator cria exatamente 30 ideias.
10. Usuário seleciona exatamente 12.
11. Content Generator cria drafts completos.
12. Usuário revisa copy em loop até aprovar.
13. Creative Engine produz as artes.
14. Usuário revisa artes em loop com versionamento.
15. Resultado final mostra 12 peças e permite exportação.

## Páginas

- `/` — Home e projetos recentes
- `/projects/new` — início do wizard
- `/projects/{id}/brand/sources`
- `/projects/{id}/brand/analyzing`
- `/projects/{id}/brand`
- `/projects/{id}/campaign`
- `/projects/{id}/strategy`
- `/projects/{id}/ideas`
- `/projects/{id}/content`
- `/projects/{id}/production`
- `/projects/{id}/creative-review`
- `/projects/{id}/result`

## Fora de escopo

Autenticação, múltiplos usuários, Meta API, Instagram scraping, publicação, analytics, cobrança, portal de cliente, vídeos/reels, editor drag-and-drop, integrações externas de mídia, app mobile.

## Métrica de sucesso da demo

- fluxo completo sem intervenção manual no banco;
- 30 ideias geradas e 12 selecionáveis;
- 12 conteúdos completos;
- revisão textual funcionando;
- 12 artes geradas ou mockadas;
- revisão visual com pelo menos V1/V2;
- exportação final;
- possibilidade de retomar projeto existente pela Home.
