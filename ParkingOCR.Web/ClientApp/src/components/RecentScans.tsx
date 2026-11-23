import React from 'react';
import { ScanHistoryItem } from '../types';
import { History, CheckCircle, XCircle } from 'lucide-react';

interface RecentScansProps {
  history: ScanHistoryItem[];
  onSelectScan: (item: ScanHistoryItem) => void;
  activeId?: string | null;
}

export const RecentScans: React.FC<RecentScansProps> = ({
  history,
  onSelectScan,
  activeId,
}) => {
  if (history.length === 0) return null;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <History className="h-4 w-4 text-blue-400" />
          <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-300">
            Recent Scans Session History ({history.length})
          </h3>
        </div>
      </div>

      <div className="flex items-center gap-2.5 overflow-x-auto pb-2">
        {history.map((item) => {
          const isActive = item.id === activeId;
          return (
            <button
              key={item.id}
              onClick={() => onSelectScan(item)}
              className={`shrink-0 flex items-center gap-3 p-2 rounded-xl border transition-all text-left ${
                isActive
                  ? 'border-blue-500 bg-blue-500/10 shadow-md shadow-blue-500/20'
                  : 'border-slate-800 bg-slate-900/50 hover:border-slate-700 hover:bg-slate-900/80'
              }`}
            >
              <div className="h-10 w-16 rounded-lg overflow-hidden bg-slate-950 border border-slate-800 flex items-center justify-center shrink-0">
                {item.thumbnail ? (
                  <img
                    src={item.thumbnail}
                    alt={item.plateText}
                    className="h-full w-full object-cover"
                  />
                ) : (
                  <span className="text-[9px] text-slate-500">Scan</span>
                )}
              </div>

              <div className="pr-2">
                <div className="flex items-center gap-1.5">
                  {item.success ? (
                    <CheckCircle className="h-3 w-3 text-emerald-400 shrink-0" />
                  ) : (
                    <XCircle className="h-3 w-3 text-rose-400 shrink-0" />
                  )}
                  <span className="text-xs font-bold font-mono text-slate-200">
                    {item.plateText || 'Unrecognized'}
                  </span>
                </div>
                <div className="flex items-center gap-2 text-[10px] text-slate-400 mt-0.5">
                  <span>{item.timestamp}</span>
                  <span>•</span>
                  <span className="text-emerald-400">{Math.round(item.confidence * 100)}%</span>
                  <span>•</span>
                  <span>{item.durationMs}ms</span>
                </div>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
};
