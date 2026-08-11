export type ApiJob = {
  id: string;
  status: 'queued' | 'running' | 'completed' | 'completed_with_errors' | 'failed';
  progress: number;
  completedSteps: number;
  totalSteps: number;
  message: string;
  error?: string;
};

export type ApiProject = { id: string; name: string; instagramHandle?: string; websiteUrl?: string; manualContext?: string; currentStep: string; status: string; campaign?: ApiCampaign };
export type ApiCampaign = { id: string; projectId: string; name: string; objective: string; targetCount: number; context?: string };
export type ApiStrategy = { campaignName: string; strategicObjective: string; rationale: string; contentMix: { pillar: string; percentage: number }[]; pillars: { id: string; name: string; description: string }[]; targetAudiences: string[]; messages: string[]; creativeDirection: { style: string[]; recommendations: string[]; avoid: string[] }; avoid: string[] };
export type ApiIdea = { id: string; ordinal: number; title: string; pillar: string; contentType: string; description: string; promise?: string; targetAudience?: string; creativeAngle?: string; visualDirection?: string; selected: boolean };
export type ApiContent = { contentId: string; revisionId: string; sequence: number; headline: string; supportingText?: string; caption: string; cta?: string; visualDirection?: string; version: number; isApproved: boolean };
export type ApiContentRevision = { id: string; contentItemId: string; version: number; headline: string; supportingText?: string; caption: string; cta?: string; visualDirection?: string; isApproved: boolean };
export type ApiCreativeVersion = { id: string; version: number; imageStorageKey?: string; isSelected: boolean; revisionInstruction?: string };
export type ApiBrandProfile = { visualIdentity: { colors?: { hex: string }[] }; voice: { traits?: string[]; avoid?: string[] }; audiences?: { name: string }[]; products?: { name: string }[]; contentAnalysis?: { recommendations?: string[] }; restrictions?: string[] };
export type ApiHealth = { status: string; aiMode: string };

const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5080/api';

export class ApiError extends Error {
  constructor(message: string, public readonly status?: number) { super(message); }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try { response = await fetch(`${baseUrl}${path}`, { ...init, headers: { 'Content-Type': 'application/json', ...init?.headers } }); }
  catch { throw new ApiError('Não foi possível conectar à API. Verifique se o serviço está disponível e tente novamente.'); }
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new ApiError(body.error ?? `Não foi possível concluir a solicitação (${response.status}).`, response.status);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

async function upload<T>(path: string, body: FormData): Promise<T> {
  let response: Response;
  try { response = await fetch(`${baseUrl}${path}`, { method: 'POST', body }); }
  catch { throw new ApiError('Não foi possível enviar os arquivos porque a API não está disponível.'); }
  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    throw new ApiError(payload.error ?? `Não foi possível enviar os arquivos (${response.status}).`, response.status);
  }
  return response.json() as Promise<T>;
}

export const api = {
  health: () => request<ApiHealth>('/health'),
  projects: () => request<ApiProject[]>('/projects'),
  resetDemo: () => request<void>('/demo/reset', { method: 'POST' }),
  createProject: (body: { name: string; instagramHandle?: string; websiteUrl?: string; manualContext?: string }) => request<ApiProject>('/projects', { method: 'POST', body: JSON.stringify(body) }),
  project: (projectId: string) => request<ApiProject & { campaign?: ApiCampaign }>(`/projects/${projectId}`),
  uploadSources: (projectId: string, files: File[]) => { const form = new FormData(); files.forEach(file => form.append('files', file, file.name)); return upload(`/projects/${projectId}/sources`, form); },
  analyzeBrand: (projectId: string) => request<ApiJob>(`/projects/${projectId}/brand/analyze`, { method: 'POST' }),
  brand: (projectId: string) => request<ApiBrandProfile>(`/projects/${projectId}/brand`),
  saveBrand: (projectId: string, profile: ApiBrandProfile) => request<ApiBrandProfile>(`/projects/${projectId}/brand`, { method: 'PUT', body: JSON.stringify(profile) }),
  approveBrand: (projectId: string) => request(`/projects/${projectId}/brand/approve`, { method: 'POST' }),
  createCampaign: (projectId: string, body: { name: string; objective?: string; targetCount: number; context?: string }) => request<ApiCampaign>(`/projects/${projectId}/campaign`, { method: 'POST', body: JSON.stringify(body) }),
  generateStrategy: (campaignId: string) => request<ApiJob>(`/campaigns/${campaignId}/strategy/generate`, { method: 'POST' }),
  strategy: (campaignId: string) => request<ApiStrategy>(`/campaigns/${campaignId}/strategy`),
  approveStrategy: (campaignId: string) => request(`/campaigns/${campaignId}/strategy/approve`, { method: 'POST' }),
  generateIdeas: (campaignId: string) => request<ApiJob>(`/campaigns/${campaignId}/ideas/generate`, { method: 'POST' }),
  ideas: (campaignId: string) => request<ApiIdea[]>(`/campaigns/${campaignId}/ideas`),
  selectIdeas: (campaignId: string, ideaIds: string[]) => request<ApiIdea[]>(`/campaigns/${campaignId}/ideas/select`, { method: 'POST', body: JSON.stringify({ ideaIds }) }),
  generateContent: (campaignId: string) => request<ApiJob>(`/campaigns/${campaignId}/content/generate`, { method: 'POST' }),
  activeContentJob: (campaignId: string) => request<ApiJob | undefined>(`/campaigns/${campaignId}/content/job`),
  content: (campaignId: string) => request<ApiContent[]>(`/campaigns/${campaignId}/content`),
  contentItem: (contentId: string) => request<{ item: { id: string }; revisions: ApiContentRevision[] }>(`/content/${contentId}`),
  reviseContent: (contentId: string, instruction: string) => request<ApiContentRevision>(`/content/${contentId}/revise`, { method: 'POST', body: JSON.stringify({ instruction }) }),
  approveRevision: (contentId: string, revisionId: string) => request<ApiContentRevision>(`/content/${contentId}/revision/${revisionId}/approve`, { method: 'POST' }),
  generateCreatives: (campaignId: string) => request<ApiJob>(`/campaigns/${campaignId}/creatives/generate`, { method: 'POST' }),
  creatives: (contentId: string) => request<ApiCreativeVersion[]>(`/content/${contentId}/creatives`),
  selectCreative: (contentId: string, versionId: string) => request<ApiCreativeVersion>(`/content/${contentId}/creative/${versionId}/select`, { method: 'POST' }),
  reviseCreative: (contentId: string, instruction: string) => request<ApiJob>(`/content/${contentId}/creative/revise`, { method: 'POST', body: JSON.stringify({ instruction }) }),
  job: (id: string) => request<ApiJob>(`/jobs/${id}`),
  exportUrl: (projectId: string) => `${baseUrl}/projects/${projectId}/export`,
  assetUrl: (storageKey: string) => `${baseUrl.replace(/\/api$/, '')}/assets/${storageKey}`,
};

export async function waitForJob(job: ApiJob, onUpdate: (job: ApiJob) => void): Promise<ApiJob> {
  let current = job;
  let consecutiveNetworkFailures = 0;
  onUpdate(current);
  while (current.status === 'queued' || current.status === 'running') {
    await new Promise(resolve => window.setTimeout(resolve, 700));
    try {
      current = await api.job(current.id);
      consecutiveNetworkFailures = 0;
      onUpdate(current);
    } catch (reason) {
      consecutiveNetworkFailures += 1;
      onUpdate({ ...current, message: `Conexão instável. Tentando novamente (${consecutiveNetworkFailures}/8)…` });
      if (consecutiveNetworkFailures >= 8) throw new ApiError(`Não foi possível consultar o processamento. Verifique a conexão com a API e tente novamente. Job: ${current.id}`);
      await new Promise(resolve => window.setTimeout(resolve, 1000 * consecutiveNetworkFailures));
    }
  }
  if (current.status === 'failed') throw new ApiError(current.error ?? current.message);
  return current;
}
