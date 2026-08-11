# Backlog técnico — integração oficial Meta

## Estado

**Não iniciado.** Este documento registra uma evolução futura; não autoriza
alterações no fluxo atual do RED AI.

## Objetivo futuro

Permitir que uma conta profissional autorizada do cliente forneça dados e
assets reais ao projeto, como complemento de uploads manuais e análise do
site. A integração deve usar somente APIs oficiais da Meta.

## Escopo inicial recomendado

- OAuth oficial para conectar uma Página do Facebook e uma conta profissional
  do Instagram associada.
- Leitura de perfil, mídias recentes e métricas permitidas pela autorização.
- Importação de imagens selecionadas para o armazenamento existente como
  `BrandSource`, com origem, URL remota, data e metadados de licença/contexto.
- Uso desses assets como evidência para Brand DNA e para o
  `AuthenticityGuard` do pipeline criativo.
- O site e os uploads manuais continuam fontes válidas quando a Meta não for
  conectada.

## Fora de escopo inicial

- Scraping de perfis públicos.
- Login como requisito para o RED AI.
- Publicação automática em Instagram ou Facebook.
- Comentários, mensagens, webhooks ou gestão de comunidade.
- Marketing API, anúncios, faturamento ou métricas de campanhas pagas.
- Acesso a contas pessoais ou a contas não autorizadas pelo cliente.

## Requisitos de segurança e privacidade

- Nunca expor access tokens ao frontend.
- Armazenar tokens cifrados no backend e registrar expiração/revogação.
- Associar cada conexão a um projeto/cliente e permitir desconexão.
- Aplicar menor conjunto de permissões necessário.
- Implementar paginação, rate-limit, tratamento de token expirado e auditoria
  mínima de sincronização.
- Não preservar mídias importadas além da finalidade autorizada pelo cliente.

## Arquitetura sugerida

1. `MetaConnection` no backend encapsula OAuth, token e IDs externos.
2. `IMetaSourceImporter` sincroniza dados de forma assíncrona.
3. O importador cria `BrandSource` usando a abstração de storage existente.
4. O pipeline de marca/arte consome apenas `BrandSource`; ele não chama a Meta
   diretamente.
5. Assets importados devem receber uma classificação explícita, por exemplo:
   `logo`, `product`, `location`, `team`, `generic-reference`.

## Relação com autenticidade criativa

Uma foto importada da Meta só pode ser usada como asset autêntico quando sua
classificação for compatível com o conceito. Sem uma foto adequada, posts que
alegam mostrar espaço, equipe, produto ou serviço real devem cair em uma
alternativa tipográfica/abstrata, e não em uma imagem fictícia.

## Critérios para iniciar

- App Meta configurado e em modo apropriado para testes.
- Conta Instagram profissional e Página vinculada para um projeto de teste.
- Política de privacidade e fluxo de consentimento definidos.
- Permissões finais confirmadas na documentação oficial da Meta e, quando
  necessário, aprovadas no App Review.
- Uma decisão explícita sobre retenção e remoção de imagens sincronizadas.

## Ordem de execução futura

1. Spike técnico com uma conta de teste, somente leitura.
2. Persistência segura e desconexão.
3. Importação manual de fontes selecionadas.
4. Sincronização assíncrona e observabilidade.
5. Integração com Brand DNA e Authenticity Guard.

