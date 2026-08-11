import type { Step } from '../../hooks/useRedAIWizard';

const groups: [string, Step[]][] = [
  ['01 Marca', ['sources', 'analyzing', 'brand']], ['02 Estratégia', ['brief', 'strategy']],
  ['03 Ideias', ['ideas']], ['04 Conteúdo', ['content']], ['05 Artes', ['production', 'creatives']], ['06 Resultado', ['result']],
];

export default function WizardProgress({ step }: { step: Step }) {
  return <div className="progress">{groups.map(([name, mapped], index) => <span className={mapped.includes(step) ? 'now' : groups.slice(0, index).some(([, values]) => values.includes(step)) ? 'past' : ''} key={name}>{name}</span>)}</div>;
}
