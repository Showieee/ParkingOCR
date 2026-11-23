import React from 'react';
import { ShieldCheck, Cpu, Zap, Car } from 'lucide-react';

interface NavbarProps {
  isHealthy: boolean | null;
}

export const Navbar: React.FC<NavbarProps> = ({ isHealthy }) => {
  return (
    <header className="border-b border-slate-800 bg-slate-900/80 backdrop-blur-md sticky top-0 z-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <div className="flex items-center space-x-3">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-tr from-blue-600 via-indigo-600 to-cyan-400 p-0.5 shadow-lg shadow-blue-500/20 flex items-center justify-center">
            <div className="h-full w-full bg-slate-950 rounded-[10px] flex items-center justify-center">
              <Car className="h-5 w-5 text-blue-400" />
            </div>
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <h1 className="text-lg font-bold tracking-tight text-white flex items-center gap-1.5">
                Parking<span className="text-blue-400">OCR</span>
              </h1>
              <span className="text-[11px] font-semibold uppercase px-2 py-0.5 rounded-full bg-blue-500/10 text-blue-400 border border-blue-500/20">
                Romanian LPR
              </span>
            </div>
            <p className="text-xs text-slate-400">
              Offline Deep License Plate Recognition & Country Strip Filter
            </p>
          </div>
        </div>

        <div className="hidden md:flex items-center space-x-4">
          <div className="flex items-center space-x-2 text-xs text-slate-400 bg-slate-950/60 border border-slate-800 rounded-lg px-3 py-1.5">
            <Cpu className="h-3.5 w-3.5 text-cyan-400" />
            <span>OpenCvSharp4</span>
            <span className="text-slate-600">•</span>
            <Zap className="h-3.5 w-3.5 text-amber-400" />
            <span>ONNX DIN 1451</span>
            <span className="text-slate-600">•</span>
            <span className="font-medium text-emerald-400">.NET 9</span>
          </div>

          <div className="flex items-center space-x-2">
            <span
              className={`h-2.5 w-2.5 rounded-full ${
                isHealthy === true
                  ? 'bg-emerald-500 shadow-md shadow-emerald-500/50 animate-pulse'
                  : isHealthy === false
                  ? 'bg-rose-500'
                  : 'bg-amber-500'
              }`}
            />
            <span className="text-xs font-medium text-slate-300">
              {isHealthy === true ? 'Engine Ready' : isHealthy === false ? 'Offline' : 'Connecting...'}
            </span>
          </div>
        </div>
      </div>
    </header>
  );
};
