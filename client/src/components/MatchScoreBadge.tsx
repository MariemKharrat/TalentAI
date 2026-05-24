import { MatchLevel } from '../types';

interface MatchScoreBadgeProps {
  score: number;
  level: MatchLevel;
}

const badgeStyles: Record<MatchLevel, { background: string; color: string; border: string }> = {
  [MatchLevel.High]: { background: 'var(--success-bg)', color: 'var(--success-color)', border: '1px solid var(--success-border)' },
  [MatchLevel.Medium]: { background: 'var(--warning-bg)', color: 'var(--warning-color)', border: '1px solid var(--warning-border)' },
  [MatchLevel.Low]: { background: 'var(--danger-bg)', color: 'var(--danger-color)', border: '1px solid var(--danger-border)' },
};

function MatchScoreBadge({ score, level }: MatchScoreBadgeProps) {
  const style = badgeStyles[level];

  return (
    <span
      style={{
        ...style,
        borderRadius: '999px',
        display: 'inline-flex',
        alignItems: 'center',
        gap: '0.35rem',
        fontSize: '0.875rem',
        fontWeight: 700,
        padding: '0.4rem 0.8rem',
      }}
    >
      {Math.round(score)}% · {level}
    </span>
  );
}

export default MatchScoreBadge;
