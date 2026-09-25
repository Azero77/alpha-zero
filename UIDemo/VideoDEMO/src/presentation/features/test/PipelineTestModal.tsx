import React, { useState } from 'react';
import { Play, CheckCircle2, XCircle, Clock, Loader2, X, Terminal, Cpu } from 'lucide-react';
import { PipelineTestRunner, type TestStepResult } from '../../../testing/pipeline-test-runner';
import clsx from 'clsx';

interface PipelineTestModalProps {
  isOpen: boolean;
  onClose: () => void;
  sampleVideoId?: string;
}

export const PipelineTestModal: React.FC<PipelineTestModalProps> = ({
  isOpen,
  onClose,
  sampleVideoId,
}) => {
  const [isRunning, setIsRunning] = useState(false);
  const [results, setResults] = useState<TestStepResult[]>([]);

  const handleRun = async () => {
    setIsRunning(true);
    const runner = new PipelineTestRunner();
    runner.onUpdate = (steps: TestStepResult[]) => setResults(steps);

    try {
      await runner.runAll(sampleVideoId);
    } finally {
      setIsRunning(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
      <div className="bg-white rounded-2xl max-w-xl w-full p-6 shadow-2xl border border-slate-200">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2">
            <div className="p-1.5 bg-slate-900 text-white rounded-lg">
              <Cpu size={18} />
            </div>
            <div>
              <h3 className="text-sm font-bold text-slate-900">Ingestion Pipeline Test Suite</h3>
              <p className="text-[11px] text-slate-500">
                Validates contracts against CDK VideoPipelineConstruct & FastEndpoints API
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            disabled={isRunning}
            className="text-slate-400 hover:text-slate-700 disabled:opacity-40"
          >
            <X size={18} />
          </button>
        </div>

        <div className="py-4 space-y-3">
          {results.length === 0 && !isRunning && (
            <div className="text-center py-8 text-xs text-slate-500 space-y-2">
              <Terminal size={32} className="mx-auto text-slate-300" />
              <p className="font-medium">Run synthetic end-to-end tests against the video pipeline:</p>
              <ul className="text-slate-400 text-[11px] space-y-1 max-w-xs mx-auto text-left list-disc list-inside">
                <li>Video & Thumbnail presigned PUT upload contract</li>
                <li>SignalR `/hubs/video-progress` WebSocket connection</li>
                <li>Key retrieval `/api/video/keys` (AES-128 binary)</li>
                <li>Step Functions Saga state persistence</li>
              </ul>
            </div>
          )}

          {results.map((r) => (
            <div
              key={r.step}
              className={clsx(
                'p-3 rounded-xl border text-xs transition-colors',
                r.status === 'passed' && 'bg-emerald-50/50 border-emerald-200 text-emerald-900',
                r.status === 'failed' && 'bg-red-50/50 border-red-200 text-red-900',
                r.status === 'running' && 'bg-amber-50/50 border-amber-200 text-amber-900',
                r.status === 'pending' && 'bg-slate-50 border-slate-200 text-slate-600',
                r.status === 'skipped' && 'bg-slate-50 border-slate-200 text-slate-400'
              )}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 font-medium">
                  {r.status === 'running' && <Loader2 size={14} className="animate-spin text-amber-600" />}
                  {r.status === 'passed' && <CheckCircle2 size={14} className="text-emerald-600" />}
                  {r.status === 'failed' && <XCircle size={14} className="text-red-600" />}
                  {r.status === 'pending' && <Clock size={14} className="text-slate-400" />}
                  {r.status === 'skipped' && <Clock size={14} className="text-slate-300" />}
                  <span>Step {r.step}: {r.name}</span>
                </div>
                {r.durationMs != null && (
                  <span className="text-[10px] text-slate-400 font-mono">{r.durationMs}ms</span>
                )}
              </div>
              {r.details && (
                <p className="mt-1.5 text-[11px] text-slate-600 pl-5 font-mono break-all">
                  {r.details}
                </p>
              )}
            </div>
          ))}
        </div>

        <div className="pt-3 border-t border-slate-100 flex items-center justify-between">
          <span className="text-[11px] text-slate-400">
            {sampleVideoId ? `Testing with sample: ${sampleVideoId.slice(0, 8)}...` : 'Using synthetic test payload'}
          </span>
          <div className="flex items-center gap-2">
            <button
              onClick={onClose}
              disabled={isRunning}
              className="px-3 py-1.5 text-xs text-slate-600 hover:bg-slate-100 rounded-lg transition-colors"
            >
              Close
            </button>
            <button
              onClick={handleRun}
              disabled={isRunning}
              className="flex items-center gap-1.5 px-4 py-1.5 text-xs font-semibold bg-slate-900 text-white rounded-lg hover:bg-slate-800 disabled:opacity-40 transition-colors"
            >
              {isRunning ? (
                <>
                  <Loader2 size={14} className="animate-spin" />
                  Running Tests...
                </>
              ) : (
                <>
                  <Play size={14} />
                  Run Pipeline Tests
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
