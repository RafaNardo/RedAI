# 03 — Architecture

## Princípio

UX mínima, núcleo reaproveitável. O Wizard é somente um cliente das APIs. Futuramente dashboard SaaS, portal de cliente, publicação e integrações reutilizam os mesmos módulos.

## Topologia

```text
Next.js Wizard
      |
      v
ASP.NET Core API
      |
+-----+---------+-------------+
|               |             |
PostgreSQL   Asset Storage   Background Jobs
                              |
                              v
                          OpenAI API
```

## Estrutura sugerida

```text
red-ai/
  apps/web/
  src/
    RedAI.Api/
    RedAI.Application/
    RedAI.Domain/
    RedAI.Infrastructure/
  tests/
    RedAI.UnitTests/
    RedAI.IntegrationTests/
  demo/
  docs/
```

## Agregado principal

```text
Project
  ├── BrandSources[]
  ├── BrandProfile
  └── Campaign
       ├── CampaignStrategy
       ├── ContentIdeas[]
       └── ContentItems[]
            ├── ContentRevisions[]
            └── CreativeVersions[]
```

## Módulos

### Projects
Criação, listagem, retomada e `currentStep`.

### Brand Intelligence
Ingestão de fontes, website snapshot, análise multimodal e aprovação de Brand DNA.

### Campaign Intelligence
Briefing, estratégia e 30 ideias.

### Content Intelligence
Geração das 12 copies e revisões iterativas.

### Creative Engine
CreativeBrief, layout selection, visual generation, renderer, versionamento e revisão.

### Jobs
Operações longas e progresso.

### Storage
Interface `IAssetStorage`.

### AI
`IAIClient`, schemas, logging em `AIRun` e domínio dos prompts.

## Storage / CDN

Nunca guardar caminhos locais no domínio. Guardar `storage_key`.

Interface:

```csharp
public interface IAssetStorage
{
    Task<StoredAsset> PutAsync(Stream stream, string key, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    string GetPublicUrl(string key);
}
```

Implementações:
- `LocalAssetStorage` no desenvolvimento
- `S3AssetStorage` mais tarde (AWS S3, Cloudflare R2, MinIO etc.)

Estrutura lógica:

```text
/projects/{projectId}/sources/...
/projects/{projectId}/campaign/... 
/projects/{projectId}/content/{contentId}/visuals/...
/projects/{projectId}/content/{contentId}/creatives/v1/final.png
```

CDN futura fica na frente do storage sem mudar entidades.

## Renderer

Preferência para MVP: HTML/CSS -> Playwright screenshot com viewport 1080x1350.

Benefícios:
- tipografia precisa
- reuso de CSS
- fácil criação de templates
- texto nunca depende de modelo de imagem

Alternativa aceita: SVG -> PNG.

## Async jobs

Endpoints longos retornam `202` + `jobId`.

Frontend faz polling em `/api/jobs/{jobId}` a ~1–2s.

Geração das 12 artes: concorrência configurável, default 3.

Retry somente para 429, timeout e 5xx, até 3 tentativas com backoff.

## Evolução futura

Adicionar usuários/workspaces envolve adicionar ownership acima de `Project`, sem mudar pipeline.

Adicionar Meta envolve `IPublishingProvider` e `ISocialSourceProvider`, sem mudar Brand DNA/Campaign/Creative.
