'use client';

import { HomeStep, WizardShell } from './wizardSteps';
import { useRedAIWizard } from './useRedAIWizard';

export default function Page() {
  const wizard = useRedAIWizard();
  return wizard.step === 'home' ? <HomeStep wizard={wizard} /> : <WizardShell wizard={wizard} />;
}
