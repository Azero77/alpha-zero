import React, { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Video, FileText, ChevronLeft, CheckCircle2, Lock, Play } from 'lucide-react';
import { api } from '../../api';
import { clsx } from 'clsx';
import { motion, AnimatePresence } from 'framer-motion';

import { VideoPlayer } from '../../components/video/VideoPlayer';
import { useMemo } from 'react';

const VideoPlayerContainer = ({ item }: { item: any }) => {
  const { data: streamingInfo, isLoading } = useQuery({
    queryKey: ['streaming', item.resourceId],
    queryFn: () => (api as any).getStreamingInfo(item.resourceId),
    enabled: !!item.resourceId && (item.metadata?.Status === 'Ready' || item.metadata?.Status === 'Published')
  });

  const playerConfig = useMemo(() => {
    if (!streamingInfo) return null;
    return {
      manifestUrl: streamingInfo.url,
      posterUrl: item.metadata?.ThumbnailUrl,
      drm: streamingInfo.drm,
      clearKey: streamingInfo.key ? {
        keyId: streamingInfo.key,
        key: streamingInfo.key
      } : undefined
    };
  }, [streamingInfo, item]);

  if (isLoading) {
    return (
      <div className="aspect-video bg-slate-900 rounded-2xl flex flex-col items-center justify-center gap-4 border border-slate-800 animate-pulse">
         <Play className="text-slate-800" size={48} />
         <span className="text-[10px] font-black uppercase tracking-[0.2em] text-slate-600">Initializing Secure Stream...</span>
      </div>
    );
  }

  if (!playerConfig) {
    return (
      <div className="aspect-video bg-slate-100 dark:bg-slate-900 rounded-2xl flex flex-col items-center justify-center gap-4 border border-slate-200 dark:border-slate-800">
         <Lock className="text-slate-300 dark:text-slate-700" size={48} />
         <span className="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">Content Processing or Unauthorized</span>
      </div>
    );
  }

  return (
    <div className="relative group">
       <VideoPlayer config={playerConfig} className="rounded-2xl shadow-2xl overflow-hidden border border-slate-200 dark:border-slate-800" />
       {/* Progress Bitmask Overlay */}
       <div className="absolute top-6 right-6 px-4 py-2 glass rounded-full text-[10px] font-black uppercase tracking-widest text-slate-900 dark:text-white pointer-events-none opacity-0 group-hover:opacity-100 transition-opacity">
          {item.metadata?.Status || 'Ready'}
       </div>
    </div>
  );
};

export const CourseViewer: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  const { data: course, isLoading } = useQuery({
    queryKey: ['course', id],
    queryFn: () => api.getCourse(id!),
    enabled: !!id
  });

  const selectedItem = course?.sections
    .flatMap(s => s.items)
    .find(i => i.id === selectedItemId) || course?.sections[0]?.items[0];

  if (isLoading) return <div className="p-20 text-center text-slate-400 font-bold uppercase tracking-widest animate-pulse">Syncing Academy Content...</div>;
  if (!course) return <div>Resource not found.</div>;

  return (
    <div className="flex h-[calc(100vh-10rem)] -m-10">
      {/* Content Sidebar */}
      <aside className="w-80 border-r border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/30 flex flex-col h-full overflow-hidden">
        <div className="p-6 border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950">
          <Link to="/learn" className="flex items-center gap-2 text-[10px] font-bold text-slate-400 uppercase tracking-[0.2em] mb-4 hover:text-slate-900 dark:hover:text-white transition-colors">
            <ChevronLeft size={14} strokeWidth={3} />
            Back to Dashboard
          </Link>
          <h2 className="text-lg font-black tracking-tight leading-tight">{course.title}</h2>
          <div className="mt-4 h-1.5 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
             <div className="h-full bg-green-500 w-1/3" />
          </div>
        </div>

        <div className="flex-1 overflow-y-auto p-4 space-y-8 custom-scrollbar">
          {course.sections.map((section) => (
            <div key={section.id} className="space-y-3">
              <h4 className="px-3 text-[10px] font-black text-slate-400 uppercase tracking-[0.2em]">{section.title}</h4>
              <div className="space-y-1">
                {section.items.map((item) => (
                  <button
                    key={item.id}
                    onClick={() => setSelectedItemId(item.id)}
                    className={clsx(
                      "w-full text-left px-3 py-3 rounded-xl transition-all group relative",
                      selectedItem?.id === item.id 
                        ? "bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 shadow-sm" 
                        : "hover:bg-slate-100 dark:hover:bg-slate-900"
                    )}
                  >
                    <div className="flex items-start gap-3">
                      <div className={clsx(
                        "mt-1 w-2 h-2 rounded-full",
                        item.id === selectedItemId ? "bg-primary-600 shadow-[0_0_8px_rgba(0,112,243,0.5)]" : "bg-slate-300 dark:bg-slate-700"
                      )} />
                      <div className="flex-1 space-y-1">
                        <p className={clsx("text-xs font-bold leading-snug", 
                          selectedItem?.id === item.id ? "text-slate-900 dark:text-white" : "text-slate-500")}>
                          {item.title}
                        </p>
                        <div className="flex items-center gap-3">
                           <span className="text-[9px] font-black uppercase tracking-widest text-slate-400 opacity-60">
                             {item.type}
                           </span>
                           {item.metadata?.Duration && (
                             <span className="text-[9px] font-mono text-slate-400">{item.metadata.Duration}</span>
                           )}
                        </div>
                      </div>
                      {item.metadata?.Status === 'Ready' || item.metadata?.Status === 'Published' ? (
                        <CheckCircle2 size={14} className="text-green-500" />
                      ) : (
                        <Lock size={14} className="text-slate-300" />
                      )}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 bg-white dark:bg-slate-950 overflow-y-auto">
         <AnimatePresence mode="wait">
           {selectedItem ? (
             <motion.div 
               key={selectedItem.id}
               initial={{ opacity: 0, y: 10 }}
               animate={{ opacity: 1, y: 0 }}
               exit={{ opacity: 0, y: -10 }}
               className="p-16 max-w-4xl mx-auto space-y-12"
             >
                <div className="space-y-4">
                  <div className="flex items-center gap-3 text-primary-600 font-bold uppercase tracking-[0.2em] text-[10px]">
                    {selectedItem.type === 'Lesson' ? <Video size={14} /> : <FileText size={14} />}
                    <span>{selectedItem.type}</span>
                  </div>
                  <h1 className="text-4xl font-black tracking-tighter leading-tight">{selectedItem.title}</h1>
                </div>

                {selectedItem.type === 'Lesson' ? (
                  <div className="space-y-8">
                    <VideoPlayerContainer item={selectedItem} />
                    <div className="prose dark:prose-invert max-w-none">
                      <p className="text-slate-500 font-medium leading-relaxed italic">
                        In this module, we explore the fundamental principles of {selectedItem.title}. 
                        This resource is served via the global edge network for optimal performance.
                      </p>
                    </div>
                  </div>
                ) : (
                  <div className="bg-slate-50 dark:bg-slate-900/40 border border-slate-200 dark:border-slate-800 rounded-3xl p-12 space-y-10">
                    <div className="flex justify-between items-start">
                       <div className="space-y-2">
                         <h3 className="text-xl font-bold uppercase tracking-tight">Final Assessment</h3>
                         <p className="text-sm text-slate-500 font-medium">Verify your understanding of the core concepts.</p>
                       </div>
                       <div className="w-16 h-16 border-2 border-primary-500 rounded-2xl flex flex-col items-center justify-center text-primary-600">
                          <span className="text-lg font-black leading-none">{selectedItem.metadata?.PassingScore || 70}</span>
                          <span className="text-[8px] font-bold uppercase">Pass</span>
                       </div>
                    </div>

                    <div className="grid grid-cols-2 gap-6">
                       <div className="p-6 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-2xl space-y-2">
                         <div className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">Type</div>
                         <p className="text-sm font-black uppercase">{selectedItem.metadata?.Type || 'MCQ'}</p>
                       </div>
                       <div className="p-6 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-2xl space-y-2">
                         <div className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">Questions</div>
                         <p className="text-sm font-black uppercase">12 Units</p>
                       </div>
                    </div>

                    <button className="w-full btn btn-primary h-14 uppercase tracking-[0.2em] font-black text-xs shadow-xl shadow-slate-900/10">
                      Begin Assessment
                    </button>
                  </div>
                )}
             </motion.div>
           ) : (
             <div className="flex items-center justify-center h-full text-slate-300 font-bold uppercase tracking-[0.2em] text-xs">
                Select an item to begin learning
             </div>
           )}
         </AnimatePresence>
      </main>
    </div>
  );
};
