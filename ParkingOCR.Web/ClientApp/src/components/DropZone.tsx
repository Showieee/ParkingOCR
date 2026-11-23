import React, { useState, useRef, useCallback } from 'react';
import { UploadCloud, Image as ImageIcon, X, AlertCircle } from 'lucide-react';

interface DropZoneProps {
  onImageSelected: (file: File | null, base64: string | null) => void;
  selectedPreview: string | null;
  isLoading: boolean;
  onClear: () => void;
}

export const DropZone: React.FC<DropZoneProps> = ({
  onImageSelected,
  selectedPreview,
  isLoading,
  onClear,
}) => {
  const [isDragOver, setIsDragOver] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const processFile = useCallback(
    (file: File) => {
      setErrorMessage(null);
      if (!file.type.startsWith('image/')) {
        setErrorMessage('Please select a valid image file (JPEG, PNG, WebP).');
        return;
      }

      const reader = new FileReader();
      reader.onload = () => {
        const base64 = reader.result as string;
        onImageSelected(file, base64);
      };
      reader.readAsDataURL(file);
    },
    [onImageSelected]
  );

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      processFile(e.dataTransfer.files[0]);
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      processFile(e.target.files[0]);
    }
  };

  const handlePaste = useCallback(
    (e: React.ClipboardEvent) => {
      const items = e.clipboardData.items;
      for (let i = 0; i < items.length; i++) {
        if (items[i].type.indexOf('image') !== -1) {
          const blob = items[i].getAsFile();
          if (blob) {
            processFile(blob);
            break;
          }
        }
      }
    },
    [processFile]
  );

  return (
    <div
      onPaste={handlePaste}
      tabIndex={0}
      className="relative outline-none group"
    >
      <input
        type="file"
        ref={fileInputRef}
        onChange={handleFileChange}
        accept="image/*"
        className="hidden"
      />

      {selectedPreview ? (
        <div className="relative rounded-2xl border-2 border-slate-700/80 bg-slate-900/60 overflow-hidden p-3 shadow-xl">
          <div className="relative aspect-video max-h-80 w-full rounded-xl overflow-hidden bg-slate-950 flex items-center justify-center">
            <img
              src={selectedPreview}
              alt="Selected plate candidate"
              className="object-contain w-full h-full"
            />

            {isLoading && (
              <div className="absolute inset-0 bg-slate-950/70 backdrop-blur-sm flex flex-col items-center justify-center gap-3">
                <div className="relative">
                  <div className="h-12 w-12 rounded-full border-4 border-blue-500/20 border-t-blue-500 animate-spin" />
                  <div className="absolute inset-0 flex items-center justify-center text-xs font-mono text-blue-400">
                    OCR
                  </div>
                </div>
                <p className="text-sm font-medium text-slate-200">
                  Detecting plate & recognizing characters...
                </p>
              </div>
            )}
          </div>

          <div className="mt-3 flex items-center justify-between px-1">
            <div className="flex items-center gap-2 text-xs text-slate-400">
              <ImageIcon className="h-4 w-4 text-blue-400" />
              <span>Image loaded. Ready for OCR processing.</span>
            </div>
            <button
              onClick={(e) => {
                e.stopPropagation();
                onClear();
                if (fileInputRef.current) fileInputRef.current.value = '';
              }}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-medium text-slate-300 transition-colors"
            >
              <X className="h-3.5 w-3.5" />
              Change Image
            </button>
          </div>
        </div>
      ) : (
        <div
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={() => fileInputRef.current?.click()}
          className={`cursor-pointer rounded-2xl border-2 border-dashed transition-all p-8 sm:p-12 text-center flex flex-col items-center justify-center gap-4 ${
            isDragOver
              ? 'border-blue-500 bg-blue-500/10 shadow-lg shadow-blue-500/20'
              : 'border-slate-800 bg-slate-900/40 hover:border-slate-700 hover:bg-slate-900/60'
          }`}
        >
          <div className="h-16 w-16 rounded-2xl bg-blue-500/10 border border-blue-500/20 flex items-center justify-center text-blue-400 group-hover:scale-105 transition-transform">
            <UploadCloud className="h-8 w-8" />
          </div>

          <div>
            <h3 className="text-base font-semibold text-white">
              Drag and drop vehicle or plate image here
            </h3>
            <p className="mt-1 text-xs text-slate-400">
              Supports JPEG, PNG, BMP, WebP or paste from clipboard (Ctrl+V)
            </p>
          </div>

          <div className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-blue-600 hover:bg-blue-500 text-xs font-semibold text-white shadow-md shadow-blue-600/30 transition-colors">
            <ImageIcon className="h-4 w-4" />
            Browse Files
          </div>
        </div>
      )}

      {errorMessage && (
        <div className="mt-3 flex items-center gap-2 p-3 rounded-xl bg-rose-500/10 border border-rose-500/20 text-xs text-rose-400">
          <AlertCircle className="h-4 w-4 shrink-0" />
          <span>{errorMessage}</span>
        </div>
      )}
    </div>
  );
};
