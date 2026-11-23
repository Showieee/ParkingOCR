import React, { useState, useEffect, useCallback } from 'react';
import { Navbar } from './components/Navbar';
import { DropZone } from './components/DropZone';
import { SampleGallery } from './components/SampleGallery';
import { RecognitionResultView } from './components/RecognitionResultView';
import { RecentScans } from './components/RecentScans';
import {
  OcrRecognitionResponse,
  SampleImageDto,
  ScanHistoryItem,
} from './types';
import {
  SlidersHorizontal,
  Play,
  RotateCcw,
  Sparkles,
  Info,
  CheckCircle,
} from 'lucide-react';

export const App: React.FC = () => {
  const [isHealthy, setIsHealthy] = useState<boolean | null>(null);
  const [samples, setSamples] = useState<SampleImageDto[]>([]);
  const [selectedPreview, setSelectedPreview] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [selectedSampleId, setSelectedSampleId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [result, setResult] = useState<OcrRecognitionResponse | null>(null);
  const [history, setHistory] = useState<ScanHistoryItem[]>([]);
  const [activeHistoryId, setActiveHistoryId] = useState<string | null>(null);

  // Settings
  const [filterCountryCode, setFilterCountryCode] = useState<boolean>(true);
  const [confidenceThreshold, setConfidenceThreshold] = useState<number>(0.35);
  const [showSettings, setShowSettings] = useState<boolean>(false);

  // Health check & load samples on mount
  useEffect(() => {
    const checkHealth = async () => {
      try {
        const res = await fetch('/api/health');
        if (res.ok) {
          setIsHealthy(true);
        } else {
          setIsHealthy(false);
        }
      } catch {
        setIsHealthy(false);
      }
    };

    const fetchSamples = async () => {
      try {
        const res = await fetch('/api/ocr/samples');
        if (res.ok) {
          const data: SampleImageDto[] = await res.json();
          setSamples(data);
        }
      } catch (err) {
        console.error('Failed to load samples:', err);
      }
    };

    checkHealth();
    fetchSamples();
  }, []);

  // Run recognition for user uploaded image
  const runRecognition = useCallback(
    async (file: File | null, base64: string | null) => {
      if (!file && !base64) return;
      setIsLoading(true);

      try {
        let response: Response;

        if (file) {
          const formData = new FormData();
          formData.append('file', file);
          formData.append('filterCountryCode', String(filterCountryCode));
          formData.append('confidenceThreshold', String(confidenceThreshold));
          formData.append('drawDebugAnnotations', 'true');

          response = await fetch('/api/ocr/recognize', {
            method: 'POST',
            body: formData,
          });
        } else {
          response = await fetch('/api/ocr/recognize', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              imageBase64: base64,
              filterCountryCode,
              confidenceThreshold,
              drawDebugAnnotations: true,
            }),
          });
        }

        const data: OcrRecognitionResponse = await response.json();
        setResult(data);

        // Add to history
        const scanItem: ScanHistoryItem = {
          id: Math.random().toString(36).substring(2, 9),
          timestamp: new Date().toLocaleTimeString([], {
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
          }),
          thumbnail: data.plateCropBase64 || base64 || '',
          plateText: data.plateText,
          countryCode: data.countryCode,
          confidence: data.confidence,
          durationMs: data.processingTimeMs,
          success: data.success,
          fullResponse: data,
        };
        setHistory((prev) => [scanItem, ...prev.slice(0, 9)]);
        setActiveHistoryId(scanItem.id);
      } catch (err) {
        console.error('Recognition request error:', err);
        setResult({
          success: false,
          plateText: '',
          rawText: '',
          confidence: 0,
          confidencePercentage: '0%',
          processingTimeMs: 0,
          characters: [],
          validation: { isValid: false, formattedPlate: '', plateType: 'Error' },
          message: 'Network error or failed to reach OCR service.',
        });
      } finally {
        setIsLoading(false);
      }
    },
    [filterCountryCode, confidenceThreshold]
  );

  // Run recognition on a sample image
  const handleSelectSample = async (sample: SampleImageDto) => {
    setSelectedSampleId(sample.id);
    setSelectedFile(null);
    setSelectedPreview(sample.thumbnailBase64);
    setIsLoading(true);

    try {
      const response = await fetch(
        `/api/ocr/sample/${encodeURIComponent(sample.id)}?filterCountryCode=${filterCountryCode}&confidenceThreshold=${confidenceThreshold}`,
        { method: 'POST' }
      );
      const data: OcrRecognitionResponse = await response.json();
      setResult(data);

      const scanItem: ScanHistoryItem = {
        id: Math.random().toString(36).substring(2, 9),
        timestamp: new Date().toLocaleTimeString([], {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
        }),
        thumbnail: data.plateCropBase64 || sample.thumbnailBase64,
        plateText: data.plateText,
        countryCode: data.countryCode,
        confidence: data.confidence,
        durationMs: data.processingTimeMs,
        success: data.success,
        fullResponse: data,
      };
      setHistory((prev) => [scanItem, ...prev.slice(0, 9)]);
      setActiveHistoryId(scanItem.id);
    } catch (err) {
      console.error('Sample recognition failed:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleImageSelected = (file: File | null, base64: string | null) => {
    setSelectedFile(file);
    setSelectedPreview(base64);
    setSelectedSampleId(null);
    runRecognition(file, base64);
  };

  const handleClear = () => {
    setSelectedFile(null);
    setSelectedPreview(null);
    setSelectedSampleId(null);
    setResult(null);
  };

  const handleSelectHistoryItem = (item: ScanHistoryItem) => {
    setActiveHistoryId(item.id);
    setResult(item.fullResponse);
    if (item.fullResponse.processedImageBase64) {
      setSelectedPreview(item.fullResponse.processedImageBase64);
    } else {
      setSelectedPreview(item.thumbnail);
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 flex flex-col selection:bg-blue-500 selection:text-white">
      <Navbar isHealthy={isHealthy} />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
        {/* Hero Section */}
        <div className="text-center space-y-3 max-w-3xl mx-auto">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-500/10 border border-blue-500/20 text-blue-400 text-xs font-semibold">
            <Sparkles className="h-3.5 w-3.5 text-amber-400" />
            <span>High-Accuracy Romanian Plate OCR Engine</span>
          </div>
          <h2 className="text-3xl sm:text-4xl font-extrabold tracking-tight text-white">
            Instant License Plate Recognition & Country Strip Filtering
          </h2>
          <p className="text-sm text-slate-400 max-w-2xl mx-auto">
            Drag-and-drop vehicle images or test sample plates. Automatically detects plate coordinates,
            isolates and strips European blue 'RO' bands, and normalizes Romanian Bucharest and county formats.
          </p>
        </div>

        {/* Quick Sample Selector */}
        <div className="rounded-2xl border border-slate-800/80 bg-slate-900/40 p-4 sm:p-5">
          <SampleGallery
            samples={samples}
            isLoading={isLoading}
            onSelectSample={handleSelectSample}
            selectedSampleId={selectedSampleId}
          />
        </div>

        {/* Upload & Controls Section */}
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-300">
              Upload Image for Recognition
            </h3>

            <div className="flex items-center gap-2">
              <button
                onClick={() => setShowSettings(!showSettings)}
                className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border text-xs font-medium transition-colors ${
                  showSettings
                    ? 'border-blue-500 bg-blue-500/10 text-blue-400'
                    : 'border-slate-800 bg-slate-900/80 text-slate-400 hover:text-slate-200'
                }`}
              >
                <SlidersHorizontal className="h-3.5 w-3.5" />
                <span>Engine Settings</span>
              </button>

              {selectedPreview && (
                <button
                  disabled={isLoading}
                  onClick={() => {
                    if (selectedSampleId) {
                      const sample = samples.find((s) => s.id === selectedSampleId);
                      if (sample) handleSelectSample(sample);
                    } else {
                      runRecognition(selectedFile, selectedPreview);
                    }
                  }}
                  className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 disabled:opacity-50 text-xs font-semibold text-white shadow-md shadow-blue-600/30 transition-colors"
                >
                  <Play className="h-3.5 w-3.5 fill-current" />
                  <span>Re-scan</span>
                </button>
              )}
            </div>
          </div>

          {/* Engine Settings Drawer */}
          {showSettings && (
            <div className="rounded-xl border border-slate-800 bg-slate-900/70 p-4 grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs animate-in fade-in slide-in-from-top-2 duration-200">
              <div className="flex items-center justify-between p-3 rounded-lg bg-slate-950/60 border border-slate-800">
                <div>
                  <span className="font-semibold text-slate-200 block">
                    Filter Country Code ('RO')
                  </span>
                  <span className="text-[11px] text-slate-400">
                    Extract & strip Euroband RO from plate text
                  </span>
                </div>
                <label className="relative inline-flex items-center cursor-pointer">
                  <input
                    type="checkbox"
                    checked={filterCountryCode}
                    onChange={(e) => setFilterCountryCode(e.target.checked)}
                    className="sr-only peer"
                  />
                  <div className="w-9 h-5 bg-slate-800 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-blue-600"></div>
                </label>
              </div>

              <div className="p-3 rounded-lg bg-slate-950/60 border border-slate-800 space-y-1.5">
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-slate-200">
                    Confidence Threshold
                  </span>
                  <span className="font-mono text-blue-400 font-bold">
                    {Math.round(confidenceThreshold * 100)}%
                  </span>
                </div>
                <input
                  type="range"
                  min="0.1"
                  max="0.8"
                  step="0.05"
                  value={confidenceThreshold}
                  onChange={(e) =>
                    setConfidenceThreshold(parseFloat(e.target.value))
                  }
                  className="w-full accent-blue-500 cursor-pointer h-1.5 bg-slate-800 rounded-lg"
                />
              </div>
            </div>
          )}

          {/* Drop Zone */}
          <DropZone
            onImageSelected={handleImageSelected}
            selectedPreview={selectedPreview}
            isLoading={isLoading}
            onClear={handleClear}
          />
        </div>

        {/* Results Panel */}
        {result && (
          <div className="pt-2 animate-in fade-in duration-300">
            <RecognitionResultView
              result={result}
              originalImage={selectedPreview}
            />
          </div>
        )}

        {/* Recent Session History */}
        <RecentScans
          history={history}
          onSelectScan={handleSelectHistoryItem}
          activeId={activeHistoryId}
        />
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-800/80 bg-slate-950/90 py-6 text-center text-xs text-slate-500">
        <div className="max-w-7xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-3">
          <p>
            ParkingOCR Engine &copy; 2026. Built with OpenCvSharp4, ONNX Runtime, and ASP.NET Core (.NET 9).
          </p>
          <div className="flex items-center gap-4 text-slate-400">
            <span>DIN 1451 Mittelschrift CNN</span>
            <span>•</span>
            <span>Euroband Spatial Classifier</span>
            <span>•</span>
            <span>Bucharest & 41 Counties</span>
          </div>
        </div>
      </footer>
    </div>
  );
};
