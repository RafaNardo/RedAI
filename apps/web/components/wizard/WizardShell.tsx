import type { RedAIWizard } from '../../hooks/useRedAIWizard';
import AnalysisStep from './AnalysisStep';
import BrandStep from './BrandStep';
import CampaignBriefStep from './CampaignBriefStep';
import ContentReviewStep from './ContentReviewStep';
import CreativeReviewStep from './CreativeReviewStep';
import IdeasStep from './IdeasStep';
import IdeasGenerationStep from './IdeasGenerationStep';
import PlanningStep from './PlanningStep';
import ProductionStep from './ProductionStep';
import ResultStep from './ResultStep';
import SourcesStep from './SourcesStep';
import StrategyStep from './StrategyStep';
import WizardProgress from './WizardProgress';

export default function WizardShell({ wizard }: { wizard: RedAIWizard }) {
  const steps = { sources: SourcesStep, analyzing: AnalysisStep, brand: BrandStep, brief: CampaignBriefStep, planning: PlanningStep, strategy: StrategyStep, ideating: IdeasGenerationStep, ideas: IdeasStep, content: ContentReviewStep, production: ProductionStep, creatives: CreativeReviewStep, result: ResultStep };
  const Step = wizard.step === 'home' ? undefined : steps[wizard.step];
  return <main><header><button className="logo" onClick={() => wizard.setStep('home')}>RED <b>AI</b></button><nav><span>AI_MODE: <b>{wizard.aiMode?.toUpperCase() ?? '…'}</b></span>{wizard.installPrompt && <span className="install">Instalável</span>}<button onClick={() => wizard.setStep('home')}>Projetos</button></nav></header><WizardProgress step={wizard.step} />{wizard.error && <div className="api-error" role="alert"><span>{wizard.error}</span><button className="button secondary" onClick={() => wizard.retry?.()}>Tentar novamente</button><button aria-label="Fechar erro" onClick={() => wizard.setError(undefined)}>×</button></div>}<section className="wizard">{Step && <Step wizard={wizard} />}</section></main>;
}

export { WizardShell };
