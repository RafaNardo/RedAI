# Creative Quality + Authenticity Pass — roadmap

## Limites

Esta sequência melhora apenas o pipeline de criativos. Não implementa a
integração Meta, publicação social, editor de templates ou mudanças no funil de
campanha.

Cada milestone deve terminar com build, testes pertinentes e uma revisão do
diff antes de avançar para o próximo.

## CQ1 — Contrato e modelo de direção de arte

**Status: concluído em 2026-08-11.** `dotnet build RedAI.sln` e `dotnet test
RedAI.sln` aprovados (7 testes).

- Ampliar `creative-brief.schema.json` e o modelo C# com `visualMode`,
  `requiresAuthenticAsset`, `authenticAssetReason`, `visualDensity`,
  `negativeSpaceTarget` e `maxVisualElements`.
- Atualizar fixtures/mock e validações.
- Adicionar testes de serialização e limites do contrato.

**Gate:** `dotnet build`, `dotnet test` e, se contratos alcançarem o frontend,
`npm run build`, `npm run lint`, `npm test`.

## CQ2 — Authenticity Guard

- Identificar assets reais disponíveis no storage/`BrandSource`.
- Criar guarda de aplicação que impede representação fictícia de espaço,
  equipe, produto ou ambiente do cliente.
- Sem asset adequado, converter o brief para fallback seguro tipográfico ou
  abstrato e preservar a justificativa nos metadados.
- Cobrir cenários de academia, restaurante e lifestyle genérico.

**Gate:** testes unitários do guard + build/test da API.

## CQ3 — Direção criativa e prompt de imagem

- Atualizar prompts do Creative Director e do Visual Generator.
- Aplicar regras específicas por `visualMode` ao prompt final do GPT Image.
- Reforçar baixa densidade, hierarquia, espaço negativo e texto curto.
- Garantir que `AUTHENTIC_ASSET_REQUIRED` não chama geração de local fictício.

**Gate:** testes de composição do prompt/guard + build/test da API.

## CQ4 — Integração de fluxo e validação visual

- Propagar os metadados do brief pelo pipeline de versões criativas sem alterar
  versões já persistidas.
- Expor o aviso já disponível no fluxo, se houver, sem redesenho de interface.
- Executar casos completos de aceitação: SVR educacional, “Conheça nosso
  espaço”, restaurante sem foto e seguradora.
- Revisar manualmente os PNGs gerados em desktop e mobile.

**Gate:** build/test backend e frontend, validação de persistência/refresh e
checagem visual documentada.

## Regra de parada

Não iniciar o próximo milestone antes de registrar o resultado do gate do
milestone atual.
