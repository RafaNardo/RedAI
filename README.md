# RED AI

MVP web para transformar uma marca em uma campanha social completa, revisável e exportável. A entrega atual roda integralmente em `AI_MODE=mock`, sem chaves ou serviços externos.

## Executar

```powershell
npm install
npm run dev
```

Abra `http://localhost:3000`. O fluxo é persistido no navegador para que projetos possam ser retomados pela Home.

## Qualidade

```powershell
npm run lint
npm run test
npm run build
```

## PWA

O app inclui manifest, ícone, service worker e cache do shell. Em um navegador compatível, pode ser instalado como aplicativo. A geração no modo mock continua disponível no app instalado; integrações futuras com API continuam dependendo de conexão.

## IA e segredos

`AI_MODE=mock` é o padrão desta entrega. Não adicione chaves ao `.env` nem ao frontend. Quando a API ASP.NET Core for adicionada, configure a chave somente no ambiente de desenvolvimento:

```powershell
dotnet user-secrets set "ai-api-key" "sua-chave" --project src/RedAI.Api
```

O nome do segredo deve ser mapeado no backend para o cliente de IA, sem nunca ser enviado como `NEXT_PUBLIC_*`.

## Containers

O projeto deve ser executado com Podman, não Docker. A futura camada .NET/PostgreSQL pode usar o arquivo de exemplo em `docs/config` com `podman compose`; a interface atual não requer containers.
