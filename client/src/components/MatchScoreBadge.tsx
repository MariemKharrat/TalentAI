import { MatchLevel } from '../types';

interface MatchScoreBadgeProps {
  score: number;
  level: MatchLevel;
}

const badgeStyles: Record<MatchLevel, { background: string; color: string }> = {
  [MatchLevel.High]: { background: '#dcfce7', color: '#166534' },
  [MatchLevel.Medium]: { background: '#fef3c7', color: '#92400e' },
  [MatchLevel.Low]: { background: '#fee2e2', color: '#b91c1c' },
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
