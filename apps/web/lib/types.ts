export type Step = 'home' | 'sources' | 'analyzing' | 'brand' | 'brief' | 'strategy' | 'ideas' | 'content' | 'production' | 'creatives' | 'result';
export type Idea = { id: string; ordinal: number; title: string; pillar: string; format: string; description: string; selected: boolean };
export type Draft = { id: string; headline: string; caption: string; visual?: string; approved: boolean; versions: string[] };
export type Creative = { id: string; headline: string; approved: boolean; versions: number[]; color: string };
export type Project = { name: string; campaign: string; step: Step };
