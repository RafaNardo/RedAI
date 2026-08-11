export type ApiJob = {
  id: string;
  status: 'queued' | 'running' | 'completed' | 'completed_with_errors' | 'failed';
  progress: number;
  completedSteps: number;
  totalSteps: number;
  message: string;
};

export type ApiProject = { id: string; name: string; instagramHandle?: string; websiteUrl?: string; manualContext?: string; currentStep: string; status: string };
export type ApiCampaign = { id: string; projectId: string; name: string; objective: string; targetCount: number; context?: string };
export type ApiIdea = { id: string; ordinal: number; title: string; pillar: string; contentType: string; description: string; selected: boolean };
export type ApiContent = { id: string; sequence: number; headline: string; caption: string; cta?: string; visualDirection?: string; version: number };

const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5080/api';

export class ApiError extends Error {
  constructor(message: string, public readonly status?: number) { super(message); }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, { ...init, headers: { 'Content-Type': 'application/json', ...init?.headers } });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new ApiError(body.error ?? `Não foi possível concluir a solicitação (${response.status}).`, response.status);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

async function upload<T>(path: string, body: FormData): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, { method: 'POST', body });
  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    throw new ApiError(payload.error ?? `Não foi possível enviar os arquivos (${response.status}).`, response.status);
  }
  return response.json() as Promise<T>;
}

export const api = {
  createProject: (body: { name: string; instagramHandle?: string; websiteUrl?: string; manualContext?: string }) => request<ApiProject>('/projects', { method: 'POST', body: JSON.stringify(body) }),
  uploadSources: (projectId: string, files: File[]) => { const form = new FormData(); files.forEach(file => form.append('files', file, file.name)); return upload(`/projects/${projectId}/sources`, form); },
  analyzeBrand: (projectId: string) => request<ApiJob>(`/projects/${projectId}/brand/analyze`, { method: 'POST' }),
  approveBrand: (projectId: string) => request(`/projects/${projectId}/brand/approve`, { method: 'POST' }),
  createCampaign: (projectId: string, body: { name: string; objective?: string; targetCount: number; context?: string }) => request<ApiCampaign>(`/projects/${projectId}/campaign`, { method: 'POST', body: JSON.stringify(body) }),
  generateStrategy: (campaignId: string) => request<ApiJob>(`/campaigns/${campaignId}/strategy/generate`, { method: 'POST' }),
  approveStrategy: (campaignId: string) => request(`/campaigns/${campaignId}/strategy/approve`, { method: 'POST' }),
  generateIdeas: (campaignId: string) => request<ApiJob>(`/campaigns/${campaignId}/ideas/generate`, { method: 'POST' }),
  ideas: (campaignId: string) => request<ApiIdea[]>(`/campaigns/${campaignId}/ideas`),
  selectIdeas: (campaignId: string, ideaIds: string[]) => request<ApiIdea[]>(`/campaigns/${campaignId}/ideas/select`, { method: 'POST', body: JSON.stringify({ ideaIds }) }),
  generateContent: (campaignId: string) => request<ApiJob>(`/campaigns/${campaignId}/content/generate`, { method: 'POST' }),
  content: (campaignId: string) => request<ApiContent[]>(`/campaigns/${campaignId}/content`),
  reviseContent: (contentId: string, instruction: string) => request<{ version: number }>(`/content/${contentId}/revise`, { method: 'POST', body: JSON.stringify({ instruction }) }),
  generateCreatives: (campaignId: string) => request<ApiJob>(`/campaigns/${campaignId}/creatives/generate`, { method: 'POST' }),
  reviseCreative: (contentId: string, instruction: string) => request<ApiJob>(`/content/${contentId}/creative/revise`, { method: 'POST', body: JSON.stringify({ instruction }) }),
  job: (id: string) => request<ApiJob>(`/jobs/${id}`),
  exportUrl: (projectId: string) => `${baseUrl}/projects/${projectId}/export`,
};

export async function waitForJob(job: ApiJob, onUpdate: (job: ApiJob) => void): Promise<ApiJob> {
  let current = job;
  onUpdate(current);
  while (current.status === 'queued' || current.status === 'running') {
    await new Promise(resolve => window.setTimeout(resolve, 700));
    current = await api.job(current.id);
    onUpdate(current);
  }
  if (current.status === 'failed') throw new ApiError(current.message);
  return current;
}
