# 06 — API

Base: `/api`

## Projects

- `GET /projects`
- `POST /projects`
- `GET /projects/{id}`
- `DELETE /projects/{id}` (optional for MVP)

Create project:

```json
{
  "name": "Cassel Seguros",
  "instagramHandle": "@casselseguros",
  "websiteUrl": "https://casselseguros.com.br",
  "manualContext": "Corretora de seguros..."
}
```

## Sources

- `POST /projects/{id}/sources` multipart
- `GET /projects/{id}/sources`
- `DELETE /projects/{id}/sources/{sourceId}`

## Brand

- `POST /projects/{id}/brand/analyze` -> 202 + jobId
- `GET /projects/{id}/brand`
- `PUT /projects/{id}/brand`
- `POST /projects/{id}/brand/approve`

## Campaign

- `POST /projects/{id}/campaign`
- `GET /campaigns/{id}`
- `POST /campaigns/{id}/strategy/generate` -> 202
- `PUT /campaigns/{id}/strategy`
- `POST /campaigns/{id}/strategy/approve`

## Ideas

- `POST /campaigns/{id}/ideas/generate` -> 202
- `GET /campaigns/{id}/ideas`
- `POST /campaigns/{id}/ideas/select`
- `POST /campaigns/{id}/ideas/auto-select`
- `POST /campaigns/{id}/ideas/regenerate` -> 202

Selection request:

```json
{ "ideaIds": ["... exactly 12 ..."] }
```

Server rejects count != 12.

## Content

- `POST /campaigns/{id}/content/generate` -> 202
- `GET /campaigns/{id}/content`
- `GET /content/{id}`
- `POST /content/{id}/revise` -> creates ContentRevision
- `PUT /content/{id}/revision/{revisionId}` -> manual edit if needed
- `POST /content/{id}/revision/{revisionId}/approve`

## Creatives

- `POST /campaigns/{id}/creatives/generate` -> 202
- `GET /content/{id}/creatives`
- `POST /content/{id}/creative/revise` -> 202 or synchronous plan + job
- `POST /content/{id}/creative/{versionId}/select`

## Result

- `GET /projects/{id}/result`
- `GET /projects/{id}/export` -> ZIP

## Jobs

- `GET /jobs/{id}`

Response:

```json
{
  "id": "...",
  "status": "running",
  "progress": 58,
  "completedSteps": 7,
  "totalSteps": 12,
  "message": "Gerando arte 7 de 12"
}
```

## Dev/demo only

- `POST /demo/reset`
- `POST /demo/seed/cassel`
- `POST /demo/seed/redzone`
