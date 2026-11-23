import React from 'react';

interface ConfidenceMeterProps {
  confidence: number; // 0.0 to 1.0
  size?: 'sm' | 'md';
}

export const ConfidenceMeter: React.FC<ConfidenceMeterProps> = ({
  confidence,
  size = 'md',
}) => {
  const percentage = Math.round(confidence * 100);

  const getColor = (pct: number) => {
    if (pct >= 85) return 'text-emerald-400 bg-emerald-500';
    if (pct >= 65) return 'text-amber-400 bg-amber-500';
    return 'text-rose-400 bg-rose-500';
  };

  const colorClass = getColor(percentage);

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between text-xs">
        <span className="text-slate-400 font-medium">Model Confidence</span>
        <span className={`font-mono font-bold ${colorClass.split(' ')[0]}`}>
          {percentage}%
        </span>
      </div>
      <div className="h-2 w-full bg-slate-800 rounded-full overflow-hidden p-0.5">
        <div
          className={`h-full rounded-full transition-all duration-500 ${colorClass.split(' ')[1]}`}
          style={{ width: `${Math.max(5, percentage)}%` }}
        />
      </div>
    </div>
  );
};
