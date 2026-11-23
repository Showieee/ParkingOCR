import React from 'react';

interface PlateBadgeProps {
  plateText: string;
  countryCode?: string | null;
  plateType?: string;
  size?: 'sm' | 'md' | 'lg';
}

export const PlateBadge: React.FC<PlateBadgeProps> = ({
  plateText,
  countryCode,
  plateType = 'Standard',
  size = 'lg',
}) => {
  const isTemporary = plateType.toLowerCase().includes('temporary');
  const hasCountryStrip = Boolean(countryCode && countryCode.toUpperCase() === 'RO');

  const sizeClasses = {
    sm: 'h-10 text-lg px-2 rounded-[4px] border-[2px]',
    md: 'h-14 text-2xl px-3 rounded-[6px] border-[3px]',
    lg: 'h-20 text-4xl sm:text-5xl px-4 rounded-[8px] border-[4px]',
  }[size];

  const eurobandWidth = {
    sm: 'w-7 text-[9px]',
    md: 'w-10 text-[11px]',
    lg: 'w-14 text-[13px]',
  }[size];

  const starsSize = {
    sm: 'h-3.5 w-3.5',
    md: 'h-5 w-5',
    lg: 'h-7 w-7',
  }[size];

  return (
    <div
      className={`relative inline-flex items-center overflow-hidden font-plate font-bold tracking-wider select-none shadow-2xl transition-transform hover:scale-[1.01] ${sizeClasses} ${
        isTemporary
          ? 'bg-white border-red-600 text-red-600'
          : 'bg-white border-slate-900 text-slate-950'
      }`}
      style={{
        boxShadow:
          '0 10px 25px -5px rgba(0, 0, 0, 0.4), inset 0 2px 4px rgba(255, 255, 255, 0.9), inset 0 -2px 4px rgba(0,0,0,0.2)',
      }}
    >
      {/* Blue Euroband on the left (Romanian / EU standard) */}
      {!isTemporary && (
        <div
          className={`absolute left-0 top-0 bottom-0 bg-[#003399] flex flex-col items-center justify-between py-1 text-white border-r border-slate-900 ${eurobandWidth}`}
        >
          {/* EU Stars Circle */}
          <svg
            viewBox="0 0 100 100"
            className={`${starsSize} fill-amber-400 mt-0.5`}
          >
            <circle cx="50" cy="50" r="42" fill="none" />
            {[...Array(12)].map((_, i) => {
              const angle = (i * 30 * Math.PI) / 180;
              const cx = 50 + 35 * Math.sin(angle);
              const cy = 50 - 35 * Math.cos(angle);
              return (
                <polygon
                  key={i}
                  points={`${cx},${cy - 5} ${cx + 3.5},${cy + 4} ${cx - 5},${cy - 1.5} ${cx + 5},${cy - 1.5} ${cx - 3.5},${cy + 4}`}
                  className="fill-amber-300"
                />
              );
            })}
          </svg>
          {/* Country code 'RO' */}
          <span className="font-sans font-black tracking-normal leading-none mb-1 text-white">
            RO
          </span>
        </div>
      )}

      {/* Main plate text container */}
      <div
        className={`flex items-center justify-center font-black ${
          !isTemporary ? (size === 'lg' ? 'pl-14 pr-2' : size === 'md' ? 'pl-10 pr-1' : 'pl-7') : ''
        }`}
      >
        <span className="drop-shadow-[0_1px_1px_rgba(0,0,0,0.3)]">
          {plateText || 'RO B 123 ABC'}
        </span>
      </div>
    </div>
  );
};
