import React, { useState } from 'react';
import { OcrRecognitionResponse } from '../types';
import { PlateBadge } from './PlateBadge';
import { ConfidenceMeter } from './ConfidenceMeter';
import {
  CheckCircle2,
  XCircle,
  Clock,
  MapPin,
  Tag,
  Scan,
  Layers,
  Sparkles,
  ArrowRight,
  ShieldCheck,
  Eye,
  Maximize2,
} from 'lucide-react';

interface RecognitionResultViewProps {
  result: OcrRecognitionResponse;
  originalImage: string | null;
}

export const RecognitionResultView: React.FC<RecognitionResultViewProps> = ({
  result,
  originalImage,
}) => {
  const [activeImageTab, setActiveImageTab] = useState<'annotated' | 'crop' | 'original'>('annotated');

  if (!result.success) {
    return (
      <div className="rounded-2xl border border-rose-500/30 bg-rose-500/10 p-6 sm:p-8 text-center space-y-3">
        <div className="h-12 w-12 rounded-full bg-rose-500/20 text-rose-400 mx-auto flex items-center justify-center">
          <XCircle className="h-6 w-6" />
        </div>
        <h3 className="text-base font-semibold text-rose-300">
          No License Plate Recognized
        </h3>
        <p className="text-xs text-rose-200/80 max-w-md mx-auto">
          {result.message ||
            'Could not find a valid plate boundary or characters in the provided image. Try another angle or clearer photo.'}
        </p>
      </div>
    );
  }

  const hasCountryStrip = Boolean(result.countryCode);
  const countyName = result.validation?.county || '';
  const numbers = result.validation?.numbers || '';
  const letters = result.validation?.letters || '';

  return (
    <div className="space-y-6">
      {/* Top Banner: Embossed Badge + Instant Summary */}
      <div className="rounded-2xl border border-slate-800 bg-gradient-to-b from-slate-900/90 to-slate-900/50 p-6 sm:p-8 shadow-xl">
        <div className="flex flex-col lg:flex-row items-center justify-between gap-6">
          <div className="space-y-3 text-center lg:text-left">
            <div className="inline-flex items-center gap-2 px-2.5 py-1 rounded-full bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-xs font-semibold">
              <CheckCircle2 className="h-3.5 w-3.5" />
              <span>Plate Successfully Recognized</span>
            </div>

            <div>
              <span className="text-xs text-slate-400 block mb-1 font-medium">
                Normalized License Plate
              </span>
              <PlateBadge
                plateText={result.plateText}
                countryCode={result.countryCode}
                plateType={result.validation?.plateType}
                size="lg"
              />
            </div>
          </div>

          {/* Quick Metrics Cards */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 w-full lg:w-auto">
            <div className="rounded-xl bg-slate-950/60 border border-slate-800/80 p-3.5 flex flex-col justify-between">
              <div className="flex items-center gap-1.5 text-xs text-slate-400">
                <Scan className="h-3.5 w-3.5 text-blue-400" />
                <span>Confidence</span>
              </div>
              <p className="mt-2 text-xl font-bold font-mono text-emerald-400">
                {result.confidencePercentage}
              </p>
            </div>

            <div className="rounded-xl bg-slate-950/60 border border-slate-800/80 p-3.5 flex flex-col justify-between">
              <div className="flex items-center gap-1.5 text-xs text-slate-400">
                <Clock className="h-3.5 w-3.5 text-amber-400" />
                <span>Latency</span>
              </div>
              <p className="mt-2 text-xl font-bold font-mono text-slate-200">
                {result.processingTimeMs} ms
              </p>
            </div>

            <div className="rounded-xl bg-slate-950/60 border border-slate-800/80 p-3.5 flex flex-col justify-between">
              <div className="flex items-center gap-1.5 text-xs text-slate-400">
                <MapPin className="h-3.5 w-3.5 text-rose-400" />
                <span>County</span>
              </div>
              <p className="mt-2 text-xl font-bold text-slate-200">
                {countyName || 'Standard'}
              </p>
            </div>

            <div className="rounded-xl bg-slate-950/60 border border-slate-800/80 p-3.5 flex flex-col justify-between">
              <div className="flex items-center gap-1.5 text-xs text-slate-400">
                <Tag className="h-3.5 w-3.5 text-purple-400" />
                <span>Type</span>
              </div>
              <p className="mt-2 text-xs font-semibold text-slate-300 truncate">
                {result.validation?.plateType || 'Standard'}
              </p>
            </div>
          </div>
        </div>

        {/* Country Strip Extraction Highlight Banner */}
        <div className="mt-6 pt-5 border-t border-slate-800/80 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="h-9 w-9 rounded-lg bg-blue-500/10 border border-blue-500/20 text-blue-400 flex items-center justify-center shrink-0">
              <ShieldCheck className="h-5 w-5" />
            </div>
            <div>
              <p className="text-xs font-semibold text-slate-200 flex items-center gap-2">
                Country Strip Filtering:
                {hasCountryStrip ? (
                  <span className="text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20 text-[11px]">
                    'RO' Strip Filtered
                  </span>
                ) : (
                  <span className="text-slate-400 bg-slate-800 px-2 py-0.5 rounded text-[11px]">
                    No Country Strip
                  </span>
                )}
              </p>
              <p className="text-[11px] text-slate-400">
                {hasCountryStrip
                  ? "EU Euroband 'RO' country identifier separated from actual vehicle registration letters."
                  : 'Classic or temporary plate processed without EU blue country identifier.'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2 text-xs font-mono bg-slate-950 px-3 py-1.5 rounded-lg border border-slate-800">
            <span className="text-slate-500">Raw OCR:</span>
            <span className="text-rose-400 font-bold">{result.rawText}</span>
            <ArrowRight className="h-3 w-3 text-slate-500" />
            <span className="text-slate-500">Clean:</span>
            <span className="text-emerald-400 font-bold">{result.plateText}</span>
          </div>
        </div>
      </div>

      {/* Grid: Visual Image Inspector & Character Breakdown */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Visual Inspection Panel (7 cols) */}
        <div className="lg:col-span-7 rounded-2xl border border-slate-800 bg-slate-900/60 p-5 space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Eye className="h-4 w-4 text-cyan-400" />
              <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-300">
                Computer Vision Inspection
              </h3>
            </div>

            {/* View Switcher */}
            <div className="flex items-center bg-slate-950 p-1 rounded-lg border border-slate-800 text-xs">
              <button
                onClick={() => setActiveImageTab('annotated')}
                className={`px-3 py-1 rounded-md transition-colors ${
                  activeImageTab === 'annotated'
                    ? 'bg-blue-600 text-white font-medium'
                    : 'text-slate-400 hover:text-slate-200'
                }`}
              >
                Annotated Detection
              </button>
              {result.plateCropBase64 && (
                <button
                  onClick={() => setActiveImageTab('crop')}
                  className={`px-3 py-1 rounded-md transition-colors ${
                    activeImageTab === 'crop'
                      ? 'bg-blue-600 text-white font-medium'
                      : 'text-slate-400 hover:text-slate-200'
                  }`}
                >
                  Plate Crop
                </button>
              )}
              {originalImage && (
                <button
                  onClick={() => setActiveImageTab('original')}
                  className={`px-3 py-1 rounded-md transition-colors ${
                    activeImageTab === 'original'
                      ? 'bg-blue-600 text-white font-medium'
                      : 'text-slate-400 hover:text-slate-200'
                  }`}
                >
                  Original
                </button>
              )}
            </div>
          </div>

          {/* Active Image Canvas */}
          <div className="relative aspect-video w-full rounded-xl overflow-hidden bg-slate-950 border border-slate-800/80 flex items-center justify-center p-2">
            {activeImageTab === 'annotated' && result.processedImageBase64 ? (
              <img
                src={result.processedImageBase64}
                alt="Annotated detection"
                className="max-h-full max-w-full object-contain rounded-lg"
              />
            ) : activeImageTab === 'crop' && result.plateCropBase64 ? (
              <div className="flex flex-col items-center justify-center gap-2">
                <img
                  src={result.plateCropBase64}
                  alt="High-resolution plate crop"
                  className="max-h-48 object-contain rounded-lg shadow-2xl border border-slate-700"
                />
                <span className="text-[11px] text-slate-400">
                  Cropped plate candidate segment
                </span>
              </div>
            ) : (
              <img
                src={originalImage || ''}
                alt="Original vehicle image"
                className="max-h-full max-w-full object-contain rounded-lg"
              />
            )}
          </div>

          {result.boundingBox && (
            <div className="text-[11px] text-slate-400 flex items-center justify-between px-1">
              <span>Bounding Box: [{result.boundingBox.x}, {result.boundingBox.y}, {result.boundingBox.width}x{result.boundingBox.height}px]</span>
              <span>OpenCvSharp4 morphological gradient localization</span>
            </div>
          )}
        </div>

        {/* Character Recognition Breakdown (5 cols) */}
        <div className="lg:col-span-5 rounded-2xl border border-slate-800 bg-slate-900/60 p-5 space-y-4 flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2">
                <Layers className="h-4 w-4 text-indigo-400" />
                <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-300">
                  Character Segmentation & Classification
                </h3>
              </div>
              <span className="text-[11px] font-mono text-slate-400">
                {result.characters.length} glyphs
              </span>
            </div>

            {/* Glyph Chips */}
            <div className="grid grid-cols-4 sm:grid-cols-6 gap-2">
              {result.characters.map((char, idx) => (
                <div
                  key={idx}
                  className={`rounded-xl border p-2 flex flex-col items-center justify-between text-center transition-all ${
                    char.isCountryCode
                      ? 'bg-blue-950/40 border-blue-600/40 text-blue-300'
                      : 'bg-slate-950 border-slate-800 text-white'
                  }`}
                >
                  <span className="text-[10px] text-slate-400">
                    {char.isCountryCode ? 'Strip' : `#${idx + 1}`}
                  </span>
                  <span className="text-2xl font-black font-plate my-1">
                    {char.character}
                  </span>
                  <span className="text-[10px] font-mono text-emerald-400">
                    {Math.round(char.confidence * 100)}%
                  </span>
                </div>
              ))}
            </div>

            {/* Romanian Syntax Details */}
            <div className="mt-6 space-y-2 rounded-xl bg-slate-950/70 border border-slate-800 p-3.5 text-xs">
              <h4 className="font-semibold text-slate-300 flex items-center gap-1.5">
                <Sparkles className="h-3.5 w-3.5 text-amber-400" />
                Syntax Verification: Romanian Format
              </h4>
              <div className="grid grid-cols-3 gap-2 pt-1 font-mono text-center">
                <div className="bg-slate-900/80 p-2 rounded-lg border border-slate-800">
                  <span className="text-[10px] text-slate-500 block">County Code</span>
                  <span className="text-sm font-bold text-blue-400">{countyName || '-'}</span>
                </div>
                <div className="bg-slate-900/80 p-2 rounded-lg border border-slate-800">
                  <span className="text-[10px] text-slate-500 block">Digits</span>
                  <span className="text-sm font-bold text-amber-400">{numbers || '-'}</span>
                </div>
                <div className="bg-slate-900/80 p-2 rounded-lg border border-slate-800">
                  <span className="text-[10px] text-slate-500 block">Letters</span>
                  <span className="text-sm font-bold text-emerald-400">{letters || '-'}</span>
                </div>
              </div>
            </div>
          </div>

          <div className="pt-3 border-t border-slate-800">
            <ConfidenceMeter confidence={result.confidence} />
          </div>
        </div>
      </div>
    </div>
  );
};
