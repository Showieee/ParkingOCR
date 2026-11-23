import React from 'react';
import { SampleImageDto } from '../types';
import { Sparkles, Flag } from 'lucide-react';

interface SampleGalleryProps {
  samples: SampleImageDto[];
  isLoading: boolean;
  onSelectSample: (sample: SampleImageDto) => void;
  selectedSampleId?: string | null;
}

export const SampleGallery: React.FC<SampleGalleryProps> = ({
  samples,
  isLoading,
  onSelectSample,
  selectedSampleId,
}) => {
  if (samples.length === 0) return null;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Sparkles className="h-4 w-4 text-amber-400" />
          <h3 className="text-xs font-semibold tracking-wider uppercase text-slate-300">
            Quick Test with Real Romanian Sample Plates
          </h3>
        </div>
        <span className="text-[11px] text-slate-400">1-Click Recognition</span>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-6 gap-2.5">
        {samples.map((sample) => {
          const isSelected = selectedSampleId === sample.id;
          return (
            <button
              key={sample.id}
              disabled={isLoading}
              onClick={() => onSelectSample(sample)}
              className={`group relative flex flex-col items-center rounded-xl p-2 border text-left transition-all ${
                isSelected
                  ? 'border-blue-500 bg-blue-500/10 shadow-md shadow-blue-500/20 ring-1 ring-blue-500'
                  : 'border-slate-800 bg-slate-900/50 hover:border-slate-700 hover:bg-slate-900/80'
              } ${isLoading ? 'opacity-50 cursor-not-allowed' : ''}`}
            >
              <div className="relative aspect-[3/1] w-full rounded-lg overflow-hidden bg-slate-950 flex items-center justify-center border border-slate-800">
                {sample.thumbnailBase64 ? (
                  <img
                    src={sample.thumbnailBase64}
                    alt={sample.title}
                    className="object-contain w-full h-full group-hover:scale-105 transition-transform"
                  />
                ) : (
                  <span className="text-[10px] text-slate-500">Image</span>
                )}
                {sample.hasEuroband && (
                  <span className="absolute top-1 left-1 px-1 py-0.2 rounded bg-blue-600 text-[8px] font-bold text-white flex items-center gap-0.5 shadow">
                    <Flag className="h-2 w-2" />
                    RO
                  </span>
                )}
              </div>

              <div className="mt-2 w-full">
                <p className="text-[11px] font-bold text-slate-200 truncate">
                  {sample.expectedPlate}
                </p>
                <p className="text-[10px] text-slate-400 truncate">
                  {sample.title}
                </p>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
};
