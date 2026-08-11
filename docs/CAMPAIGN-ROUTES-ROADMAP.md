# Campaign Routes Roadmap

## Objetivo

Substituir o fluxo atual de ideias avulsas por um fluxo de campanha coerente:

`Brand DNA aprovado → estratégia → 5 rotas de campanha → escolher 1 rota → 5 posts estáticos → copies → 5 PNGs → ZIP`

Enquanto não houver suporte real a múltiplos slides, o produto não deve propor nem rotular entregáveis como carrossel, vídeo, reel, landing page ou página web.

## Regra de produto

- Uma rota é uma proposta de campanha, não uma peça de conteúdo.
- Uma campanha tem exatamente 5 rotas disponíveis.
- O usuário escolhe exatamente uma rota.
- A rota escolhida gera exatamente 5 posts estáticos relacionados.
- Um post estático gera exatamente um PNG.
- O ZIP final contém exatamente os 5 PNGs selecionados, além dos metadados técnicos.

## M8.1 — Contratos e persistência de rotas

### Escopo

- Reaproveitar a persistência atual sem exigir migração de banco, quando possível.
- Tratar o registro atualmente chamado de `ContentIdea` como uma rota de campanha durante a transição interna.
- Atualizar o schema estruturado para exigir exatamente 5 rotas.
- Cada rota deve conter: promessa, público, ângulo criativo, direção visual, pilar e formato fixo `Post estático`.
- Atualizar fixtures determinísticas do modo mock.

### Fora de escopo

- Geração de copies.
- Alterações de layout no frontend.
- Carrosséis, vídeos, reels, landing pages ou múltiplos slides.

### Gate

- API persiste exatamente 5 rotas únicas.
- Nenhuma rota aceita formato diferente de `Post estático`.
- `AI_MODE=mock` produz as mesmas 5 rotas a cada execução.
- `AI_MODE=openai` responde no schema estruturado atualizado.

## M8.2 — Escolha de rota no wizard

### Escopo

- Renomear o passo de ideias para `Rotas de Campanha` no frontend.
- Exibir cinco cards com promessa, público, ângulo e direção visual.
- Permitir selecionar somente uma rota.
- Persistir a seleção no servidor.
- Atualizar retomada de projeto e refresh para restaurar a rota escolhida.

### Gate

- O botão de continuidade só habilita com uma rota selecionada.
- Trocar a seleção persiste e sobrevive a refresh e troca de navegador.
- Projetos legados continuam abrindo; se tiverem dados do fluxo anterior, devem ser claramente tratados como legados, sem mistura silenciosa dos dois fluxos.

## M8.3 — Plano de cinco posts estáticos

### Escopo

- A partir da rota escolhida, gerar exatamente 5 briefs de post coerentes entre si.
- Cada brief representa um post estático individual, com objetivo e papel na sequência.
- Gerar as cinco copies e revisões V1 persistidas.
- Manter o job com progresso real por post persistido.

### Gate

- Exatamente 5 `ContentItem` e 5 revisões V1.
- Títulos e descrições distintos, mas alinhados à rota escolhida.
- Nenhum texto ou prompt pede slides, vídeo, landing page ou reel.
- Refresh durante o job e após a conclusão retoma o estado correto.

## M8.4 — Artes, revisão visual e exportação

### Escopo

- Renderizar uma arte PNG por post estático.
- Manter versões visuais V1/V2/V3 e o comportamento atual de revisão.
- Validar que o renderer recebe somente headline, apoio opcional e CTA; legenda permanece fora da imagem.
- Exportar os cinco PNGs finais selecionados.

### Gate

- Exatamente 5 PNGs finais no ZIP.
- Todos os arquivos possuem nomes previsíveis e não se repetem.
- Revisões de cor/layout não regeneram asset visual; troca de cena pode regenerar asset.
- Nenhuma tela promete um carrossel ou qualquer entrega que não esteja no ZIP.

## M8.5 — Demo hardening do novo funil

### Escopo

- Executar a jornada completa em `AI_MODE=mock` e `AI_MODE=openai`.
- Validar desktop, mobile e retomada de projeto.
- Revisar loaders, estados vazios, erros e retries.
- Revisar visualmente os cinco PNGs finais e o ZIP.

### Gate final

`DEMO READY = YES` somente quando:

- 5 rotas → 1 selecionada → 5 posts → 5 PNGs funcionar ponta a ponta;
- persistência PostgreSQL, refresh e retomada estiverem comprovados;
- o ZIP tiver 5 PNGs finais;
- não houver referência ou saída de carrossel, vídeo, reel ou landing page.
