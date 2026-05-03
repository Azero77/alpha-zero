import React, { useState, useCallback, useMemo } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Video as VideoIcon, Plus, Search, MoreHorizontal, Activity, ArrowUpRight, Clock, CheckCircle2, X, Image as ImageIcon } from 'lucide-react';
import { useDropzone } from 'react-dropzone';
import { api } from '../../api';
import { clsx } from 'clsx';
import { motion, AnimatePresence } from 'framer-motion';
import { VideoPlayer } from '../../components/video/VideoPlayer';

export const VideoDashboard: React.FC = () => {
  const { data: videos, isLoading } = useQuery({
    queryKey: ['videos'],
    queryFn: () => api.getVideos(),
    // Poll every 5 seconds if any video is processing
    refetchInterval: (query) => {
      const data = query.state.data as any[];
      return data?.some(v => v.status === 'Processing') ? 5000 : false;
    }
  });

  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);
  const [selectedVideo, setSelectedVideo] = useState<any | null>(null);
  const [streamingInfo, setStreamingInfo] = useState<any | null>(null);

  const handlePlay = async (video: any) => {
    try {
      const info = await (api as any).getStreamingInfo(video.id);
      setStreamingInfo(info);
      setSelectedVideo(video);
    } catch (err) {
      alert('Failed to retrieve streaming manifest.');
    }
  };

  const playerConfig = useMemo(() => {
    if (!streamingInfo || !selectedVideo) return null;
    return {
      manifestUrl: streamingInfo.url,
      posterUrl: selectedVideo.thumbnailUrl,
      drm: streamingInfo.drm,
      clearKey: streamingInfo.key ? {
        keyId: streamingInfo.key,
        key: streamingInfo.key
      } : undefined
    };
  }, [streamingInfo, selectedVideo]);

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

      {selectedVideo && playerConfig && (
        <motion.div 
          initial={{ opacity: 0, y: -20 }}
          animate={{ opacity: 1, y: 0 }}
          className="bg-slate-900 rounded-2xl overflow-hidden shadow-2xl border border-slate-800"
        >
          <div className="p-4 border-b border-slate-800 flex justify-between items-center bg-slate-900/50">
            <div className="flex items-center gap-3">
              <div className="w-2 h-2 rounded-full bg-green-500 animate-pulse" />
              <span className="text-[10px] font-black uppercase tracking-widest text-slate-400">Preview Mode: {selectedVideo.title}</span>
            </div>
            <button 
              onClick={() => { setSelectedVideo(null); setStreamingInfo(null); }}
              className="text-slate-500 hover:text-white transition-colors"
            >
              <X size={18} />
            </button>
          </div>
          <VideoPlayer config={playerConfig} className="aspect-video" />
        </motion.div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
        <PipelineStat label="Active Streams" value={videos?.filter(v => v.status === 'Ready' || v.status === 'Published').length || 0} icon={Activity} />
        <PipelineStat label="Transcoding" value={videos?.filter(v => v.status === 'Processing').length || 0} icon={Clock} isWarning />
        <PipelineStat label="Registry Nodes" value="4" icon={ArrowUpRight} />
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
                      <button 
                        onClick={() => (video.status === 'Ready' || video.status === 'Published') && handlePlay(video)}
                        disabled={video.status === 'Processing'}
                        className="w-16 h-10 rounded bg-slate-100 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex items-center justify-center overflow-hidden relative group/thumb"
                      >
                        {video.thumbnailUrl ? (
                          <img src={video.thumbnailUrl} className="w-full h-full object-cover transition-transform group-hover/thumb:scale-110" />
                        ) : (
                          <VideoIcon size={14} className="text-slate-400" />
                        )}
                        {(video.status === 'Ready' || video.status === 'Published') && (
                          <div className="absolute inset-0 bg-slate-900/40 opacity-0 group-hover/thumb:opacity-100 flex items-center justify-center transition-opacity">
                             <Activity size={14} className="text-white" />
                          </div>
                        )}
                      </button>
                      <div>
                        <p className="text-sm font-bold text-slate-900 dark:text-slate-100">{video.title}</p>
                        <p className="text-[10px] font-medium text-slate-500 truncate max-w-[240px] uppercase tracking-tighter">az:video:{video.id}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-2">
                       <div className={clsx("status-dot", 
                         (video.status === 'Ready' || video.status === 'Published') ? "status-ready" : 
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
  const [videoFile, setVideoFile] = useState<File | null>(null);
  const [thumbFile, setThumbFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [targetArn, setTargetArn] = useState('');
  const [selectedCourseId, setSelectedCourseId] = useState('');
  const queryClient = useQueryClient();

  const { data: courses } = useQuery({
    queryKey: ['courses'],
    queryFn: () => api.getCourses()
  });

  const onDropVideo = useCallback((files: File[]) => {
    if (files[0]) {
      setVideoFile(files[0]);
      if (!title) setTitle(files[0].name.split('.')[0]);
    }
  }, [title]);

  const onDropThumb = useCallback((files: File[]) => {
    if (files[0]) setThumbFile(files[0]);
  }, []);

  const { getRootProps: getVideoProps, getInputProps: getVideoInput, isDragActive: isVideoDrag } = useDropzone({
    onDrop: onDropVideo,
    accept: { 'video/mp4': ['.mp4'] },
    multiple: false
  });

  const { getRootProps: getThumbProps, getInputProps: getThumbInput, isDragActive: isThumbDrag } = useDropzone({
    onDrop: onDropThumb,
    accept: { 'image/jpeg': ['.jpg', '.jpeg'], 'image/png': ['.png'] },
    multiple: false
  });

  const handleCourseChange = (courseId: string) => {
    setSelectedCourseId(courseId);
    if (courseId) {
      // az:courses:tenant-1:course/uuid
      setTargetArn(`az:courses:tenant-1:course/${courseId}`);
    } else {
      setTargetArn('');
    }
  };

  const handleUpload = async () => {
    if (!videoFile || !title) return;
    
    try {
      setStep(2);
      // 1. Request presigned URLs (Dual if thumb present)
      const uploadInfo = await api.requestUpload({
        fileName: videoFile.name,
        title: title,
        targetResourceArn: targetArn,
        generateCustomThumbnailUrl: !!thumbFile
      });

      // 2. Upload Video
      const videoHeaders = uploadInfo.headers || uploadInfo.Headers || (uploadInfo as any).headers;
      const videoUrl = uploadInfo.preSignedUrl || uploadInfo.PreSignedUrl || (uploadInfo as any).presignedUrl;

      await (api as any).uploadFile(videoUrl, videoFile, videoHeaders, (p: number) => {
        setProgress(p * 0.8); // 80% weight for video
      });

      // 3. Upload Thumbnail if exists
      const thumbHeaders = uploadInfo.thumbnailHeaders || uploadInfo.ThumbnailHeaders || (uploadInfo as any).thumbnailHeaders;
      const thumbUrl = uploadInfo.thumbnailPreSignedUrl || uploadInfo.ThumbnailPreSignedUrl || (uploadInfo as any).thumbnailPresignedUrl;

      if (thumbFile && thumbUrl && thumbHeaders) {
        await (api as any).uploadFile(thumbUrl, thumbFile, thumbHeaders, (p: number) => {
          setProgress(80 + (p * 0.2)); // Remaining 20%
        });
      }

      setStep(3);
      queryClient.invalidateQueries({ queryKey: ['videos'] });
      setTimeout(onClose, 2000);
    } catch (error) {
      console.error('Upload failed', error);
      alert('Ingestion failure. Ensure target ARN is valid and bucket access is granted.');
      setStep(1);
    }
  };

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-6 bg-slate-950/20 dark:bg-slate-950/60 backdrop-blur-xl">
      <motion.div 
        initial={{ opacity: 0, y: 40, scale: 0.98 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        exit={{ opacity: 0, y: 40, scale: 0.98 }}
        className="bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-2xl shadow-2xl w-full max-w-2xl overflow-hidden"
      >
        <div className="p-10 space-y-8">
          {step === 1 && (
            <>
              <div className="flex justify-between items-start">
                <div className="space-y-1">
                  <h3 className="text-xl font-bold tracking-tight">Ingest Asset</h3>
                  <p className="text-sm text-slate-500 font-medium">Link video binaries to your academy hierarchy.</p>
                </div>
                <button onClick={onClose} className="p-2 hover:bg-slate-100 dark:hover:bg-slate-900 rounded-full transition-colors">
                  <X size={20} className="text-slate-400" />
                </button>
              </div>

              <div className="grid grid-cols-2 gap-8">
                <div className="space-y-6">
                  <div className="space-y-2">
                    <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Metadata Title</label>
                    <input type="text" placeholder="e.g. Chapter 4: Thermodynamics" value={title} onChange={(e) => setTitle(e.target.value)} />
                  </div>
                  <div className="space-y-2">
                    <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Target Academy</label>
                    <select 
                      value={selectedCourseId}
                      onChange={(e) => handleCourseChange(e.target.value)}
                      className="w-full h-10 px-3 py-2 text-sm rounded-md border border-slate-200 dark:border-slate-800 bg-transparent outline-none focus:ring-2 focus:ring-primary-500"
                    >
                      <option value="">Select an Academy...</option>
                      {courses?.map((c: any) => (
                        <option key={c.id} value={c.id}>{c.title}</option>
                      ))}
                    </select>
                    {targetArn && (
                      <p className="text-[9px] font-mono text-slate-400 truncate uppercase tracking-tighter">
                        {targetArn}
                      </p>
                    )}
                  </div>

                  <div {...getVideoProps()} className={clsx(
                    "border-2 border-dashed rounded-xl p-8 text-center transition-all cursor-pointer group",
                    isVideoDrag ? "border-primary-500 bg-primary-50/50" : "border-slate-100 dark:border-slate-800 hover:border-primary-500/30 bg-slate-50/50 dark:bg-slate-900/20"
                  )}>
                    <input {...getVideoInput()} />
                    <VideoIcon className={clsx("mx-auto mb-3 transition-colors", videoFile ? "text-primary-600" : "text-slate-300 group-hover:text-primary-500")} size={32} />
                    <p className="text-[10px] font-black uppercase tracking-widest text-slate-400">
                      {videoFile ? videoFile.name : 'Binary (MP4)'}
                    </p>
                  </div>
                </div>

                <div className="space-y-6">
                   <div className="space-y-2">
                     <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Custom Thumbnail (Optional)</label>
                     <div {...getThumbProps()} className={clsx(
                       "border-2 border-dashed rounded-xl h-[240px] flex flex-col items-center justify-center text-center transition-all cursor-pointer group relative overflow-hidden",
                       isThumbDrag ? "border-primary-500 bg-primary-50/50" : "border-slate-100 dark:border-slate-800 hover:border-primary-500/30 bg-slate-50/50 dark:bg-slate-900/20"
                     )}>
                        <input {...getThumbInput()} />
                        {thumbFile ? (
                          <>
                            <img src={URL.createObjectURL(thumbFile)} className="absolute inset-0 w-full h-full object-cover opacity-50" />
                            <div className="relative z-10 p-4 bg-white/90 dark:bg-slate-950/90 rounded-lg shadow-sm border border-slate-200 dark:border-slate-800">
                               <p className="text-[9px] font-black uppercase tracking-tighter">{thumbFile.name}</p>
                            </div>
                          </>
                        ) : (
                          <>
                            <ImageIcon className="text-slate-200 dark:text-slate-800 mb-2 group-hover:text-primary-500 transition-colors" size={40} />
                            <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-slate-400 px-6 leading-relaxed">Drag Poster Image</p>
                          </>
                        )}
                     </div>
                   </div>
                </div>
              </div>

              <div className="flex gap-3 pt-4 border-t border-slate-100 dark:border-slate-900">
                <button onClick={onClose} className="flex-1 btn btn-secondary uppercase tracking-widest text-[10px] font-bold h-12">Discard</button>
                <button 
                  onClick={handleUpload} 
                  disabled={!videoFile || !title}
                  className="flex-1 btn btn-primary uppercase tracking-widest text-[10px] font-bold h-12"
                >
                  Initiate Production
                </button>
              </div>
            </>
          )}

          {step === 2 && (
            <div className="py-12 space-y-10">
              <div className="space-y-2 text-center">
                <h3 className="text-2xl font-black tracking-tight uppercase">Ingesting Stream</h3>
                <p className="text-xs font-mono text-slate-400 uppercase tracking-widest">Global Node Buffer: {Math.round(progress)}%</p>
              </div>
              <div className="h-1 bg-slate-100 dark:bg-slate-900 rounded-full overflow-hidden">
                <motion.div 
                  className="h-full bg-slate-900 dark:bg-white shadow-[0_0_8px_rgba(255,255,255,0.5)]" 
                  initial={{ width: 0 }}
                  animate={{ width: `${progress}%` }}
                />
              </div>
              <div className="flex justify-center gap-12 pt-4">
                 <div className="text-center space-y-1">
                    <p className="text-[10px] font-bold text-slate-400 uppercase">Latency</p>
                    <p className="text-xs font-black">24ms</p>
                 </div>
                 <div className="text-center space-y-1">
                    <p className="text-[10px] font-bold text-slate-400 uppercase">Integrity</p>
                    <p className="text-xs font-black">Verified</p>
                 </div>
              </div>
            </div>
          )}

          {step === 3 && (
            <div className="py-12 text-center space-y-6">
              <div className="w-20 h-20 bg-slate-900 dark:bg-white text-white dark:text-slate-950 rounded-full flex items-center justify-center mx-auto shadow-2xl scale-110">
                <CheckCircle2 size={40} strokeWidth={3} />
              </div>
              <div className="space-y-1">
                <h3 className="text-xl font-bold tracking-tight">Production Queue Joined</h3>
                <p className="text-sm text-slate-500 font-medium">Assets distributed to transcoding farm. Node sync pending.</p>
              </div>
            </div>
          )}
        </div>
      </motion.div>
    </div>
  );
};

