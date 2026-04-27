import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Video as VideoIcon, Plus, Search, MoreHorizontal, Activity, ArrowUpRight, Clock, CheckCircle2 } from 'lucide-react';
import { api } from '../../api';
import { clsx } from 'clsx';
import { motion, AnimatePresence } from 'framer-motion';

export const VideoDashboard: React.FC = () => {
  const { data: videos, isLoading } = useQuery({
    queryKey: ['videos'],
    queryFn: () => api.getVideos()
  });

  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);

  return (
    <div className="space-y-12 animate-in fade-in duration-700">
      <div className="flex justify-between items-start">
        <div className="space-y-1">
          <h1 className="text-4xl font-black tracking-tight text-slate-900 dark:text-white">Production Pipeline</h1>
          <p className="text-slate-500 font-medium max-w-md">Orchestrate your academy's video assets with absolute precision.</p>
        </div>
        <button 
          onClick={() => setIsUploadModalOpen(true)}
          className="btn btn-primary gap-2"
        >
          <Plus size={18} strokeWidth={3} />
          <span>Upload Asset</span>
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
        <PipelineStat label="Active Streams" value={videos?.filter(v => v.status === 'Ready').length || 0} icon={Activity} />
        <PipelineStat label="Transcoding" value={videos?.filter(v => v.status === 'Processing').length || 0} icon={Clock} isWarning />
        <PipelineStat label="Total Volume" value="42.8 GB" icon={ArrowUpRight} />
      </div>

      <section className="space-y-4">
        <div className="flex items-center justify-between px-2">
          <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-slate-400">Master Asset Library</h3>
          <div className="relative group">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-primary-500 transition-colors" size={14} />
            <input 
              type="text" 
              placeholder="Filter assets..." 
              className="h-8 pl-9 pr-4 w-48 bg-transparent border-slate-200 dark:border-slate-800 focus:w-64 transition-all"
            />
          </div>
        </div>

        <div className="border border-slate-200 dark:border-slate-800 rounded-lg overflow-hidden bg-white dark:bg-slate-950 shadow-[0_1px_3px_rgba(0,0,0,0.05)]">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-slate-50/50 dark:bg-slate-900/50 border-b border-slate-200 dark:border-slate-800">
                <th className="px-6 py-3 text-[10px] font-bold uppercase tracking-widest text-slate-500">Resource</th>
                <th className="px-6 py-3 text-[10px] font-bold uppercase tracking-widest text-slate-500">Status</th>
                <th className="px-6 py-3 text-[10px] font-bold uppercase tracking-widest text-slate-500">Duration</th>
                <th className="px-6 py-3 text-[10px] font-bold uppercase tracking-widest text-slate-500">Pipeline ID</th>
                <th className="px-6 py-3 text-right"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-900">
              {isLoading ? (
                <tr><td colSpan={5} className="p-12 text-center text-slate-400 font-medium">Initializing library...</td></tr>
              ) : videos?.map((video) => (
                <tr key={video.id} className="hover:bg-slate-50/50 dark:hover:bg-slate-900/30 transition-colors group">
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-4">
                      <div className="w-12 h-8 rounded bg-slate-100 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex items-center justify-center overflow-hidden">
                        {video.status === 'Ready' ? (
                          <img src={`https://picsum.photos/seed/${video.id}/100/60`} className="w-full h-full object-cover grayscale group-hover:grayscale-0 transition-all" />
                        ) : (
                          <VideoIcon size={14} className="text-slate-400" />
                        )}
                      </div>
                      <div>
                        <p className="text-sm font-bold text-slate-900 dark:text-slate-100">{video.title}</p>
                        <p className="text-[10px] font-medium text-slate-500 truncate max-w-[240px] uppercase tracking-tighter">az:video:{video.id}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-2">
                       <div className={clsx("status-dot", 
                         video.status === 'Ready' ? "status-ready" : 
                         video.status === 'Processing' ? "status-processing" : "status-failed"
                       )} />
                       <span className="text-[10px] font-bold uppercase tracking-widest">{video.status}</span>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-xs font-mono text-slate-500">
                    {video.duration || '--:--'}
                  </td>
                  <td className="px-6 py-4 text-[10px] font-mono text-slate-400">
                    p-uuid-{video.id.slice(-6)}
                  </td>
                  <td className="px-6 py-4 text-right">
                    <button className="p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">
                      <MoreHorizontal size={16} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <AnimatePresence>
        {isUploadModalOpen && (
          <UploadModal onClose={() => setIsUploadModalOpen(false)} />
        )}
      </AnimatePresence>
    </div>
  );
};

const PipelineStat = ({ label, value, icon: Icon, isWarning }: any) => (
  <div className="space-y-2 border-l-2 border-slate-100 dark:border-slate-800 pl-6 py-1">
    <div className="flex items-center gap-2 text-slate-400">
      <Icon size={14} />
      <span className="text-[10px] font-bold uppercase tracking-[0.2em]">{label}</span>
    </div>
    <div className="flex items-baseline gap-2">
      <span className={clsx("text-3xl font-black tracking-tighter", isWarning ? "text-amber-500" : "text-slate-900 dark:text-white")}>{value}</span>
      {isWarning && <div className="status-dot status-processing animate-pulse" />}
    </div>
  </div>
);

const UploadModal = ({ onClose }: { onClose: () => void }) => {
  const [step, setStep] = useState(1);
  const [progress, setProgress] = useState(0);
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [targetArn, setTargetArn] = useState('');
  const queryClient = useQueryClient();

  const handleUpload = async () => {
    if (!file || !title) return;
    
    try {
      setStep(2);
      // 1. Request presigned URL from RealApiService
      const uploadInfo = await api.requestUpload({
        fileName: file.name,
        title: title,
        targetResourceArn: targetArn
      });

      // 2. Binary PUT to S3 using the new uploadFile method
      await (api as any).uploadFile(uploadInfo.preSignedUrl, file, (p: number) => {
        setProgress(p);
      });

      setStep(3);
      queryClient.invalidateQueries({ queryKey: ['videos'] });
      setTimeout(onClose, 2000);
    } catch (error) {
      console.error('Upload failed', error);
      alert('Upload failed. Check console for details.');
      setStep(1);
    }
  };

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-6 bg-white/40 dark:bg-slate-950/40 backdrop-blur-xl">
      <motion.div 
        initial={{ opacity: 0, y: 40, scale: 0.98 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        exit={{ opacity: 0, y: 40, scale: 0.98 }}
        transition={{ type: "spring", damping: 25, stiffness: 300 }}
        className="bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-xl shadow-2xl w-full max-w-lg overflow-hidden"
      >
        <div className="p-10 space-y-8">
          {step === 1 && (
            <>
              <div className="space-y-1">
                <h3 className="text-xl font-bold tracking-tight">Ingest Asset</h3>
                <p className="text-sm text-slate-500 font-medium">Specify the target resource ARN for auto-linking.</p>
              </div>

              <div className="space-y-6">
                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Metadata Title</label>
                  <input 
                    type="text" 
                    placeholder="e.g. Chapter 4: Thermodynamics" 
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Target Resource ARN</label>
                  <input 
                    type="text" 
                    className="font-mono text-xs" 
                    placeholder="az:courses:tenant-1:course/uuid/section/uuid" 
                    value={targetArn}
                    onChange={(e) => setTargetArn(e.target.value)}
                  />
                </div>
                
                <label className="block">
                  <div className="border-2 border-dashed border-slate-100 dark:border-slate-800 rounded-lg p-12 text-center group hover:border-primary-500/50 transition-colors cursor-pointer bg-slate-50/50 dark:bg-slate-900/20">
                    <input 
                      type="file" 
                      className="hidden" 
                      accept="video/*" 
                      onChange={(e) => {
                        const selectedFile = e.target.files?.[0];
                        if (selectedFile) {
                          setFile(selectedFile);
                          if (!title) setTitle(selectedFile.name.split('.')[0]);
                        }
                      }}
                    />
                    <VideoIcon className={clsx("mx-auto mb-4 transition-colors", file ? "text-primary-600" : "text-slate-300 group-hover:text-primary-500")} size={32} />
                    <p className="text-xs font-bold uppercase tracking-widest text-slate-400 group-hover:text-slate-600 transition-colors">
                      {file ? file.name : 'Click to Select Binary'}
                    </p>
                    {file && <p className="text-[10px] text-primary-600 mt-2 font-mono">{(file.size / (1024 * 1024)).toFixed(2)} MB</p>}
                  </div>
                </label>
              </div>

              <div className="flex gap-3 pt-4">
                <button onClick={onClose} className="flex-1 btn btn-secondary uppercase tracking-widest text-[10px] font-bold">Discard</button>
                <button 
                  onClick={handleUpload} 
                  disabled={!file || !title}
                  className="flex-1 btn btn-primary uppercase tracking-widest text-[10px] font-bold"
                >
                  Start Ingestion
                </button>
              </div>
            </>
          )}

          {step === 2 && (
            <div className="py-12 space-y-10">
              <div className="space-y-2 text-center">
                <h3 className="text-lg font-bold tracking-tight">Streaming Binary...</h3>
                <p className="text-xs font-mono text-slate-400 uppercase">Buffer: {progress}% Complete</p>
              </div>
              <div className="h-1 bg-slate-100 dark:bg-slate-900 rounded-full overflow-hidden">
                <motion.div 
                  className="h-full bg-slate-900 dark:bg-white" 
                  initial={{ width: 0 }}
                  animate={{ width: `${progress}%` }}
                />
              </div>
            </div>
          )}

          {step === 3 && (
            <div className="py-12 text-center space-y-6">
              <div className="w-16 h-16 bg-slate-900 dark:bg-white text-white dark:text-slate-950 rounded-full flex items-center justify-center mx-auto scale-110">
                <CheckCircle2 size={32} strokeWidth={3} />
              </div>
              <div className="space-y-1">
                <h3 className="text-lg font-bold">Ingestion Success</h3>
                <p className="text-sm text-slate-500">Video added to asynchronous transcoding pipeline.</p>
              </div>
            </div>
          )}
        </div>
      </motion.div>
    </div>
  );
};
