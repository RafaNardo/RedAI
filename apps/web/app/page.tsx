'use client';

import HomeStep from '../components/wizard/HomeStep';
import WizardShell from '../components/wizard/WizardShell';
import { useRedAIWizard } from '../hooks/useRedAIWizard';

export default function Page() {
  const wizard = useRedAIWizard();
  return wizard.step === 'home' ? <HomeStep wizard={wizard} /> : <WizardShell wizard={wizard} />;
}
