import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Plus, MoreHorizontal, GripVertical, Video, FileText, ChevronRight, ChevronDown, Trash2, ArrowRight, Layers, CheckCircle2, X } from 'lucide-react';
import { api } from '../../api';
import { clsx } from 'clsx';
import { motion, AnimatePresence } from 'framer-motion';

export const CourseArchitect: React.FC = () => {
  const queryClient = useQueryClient();
  const { data: courses, isLoading } = useQuery({
    queryKey: ['courses'],
    queryFn: () => api.getCourses()
  });

  const [selectedCourseId, setSelectedCourseId] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  return (
    <div className="flex gap-12 h-[calc(100vh-14rem)] animate-in fade-in slide-in-from-bottom-4 duration-700">
      {/* Sidebar: Course Selection */}
      <div className="w-72 flex flex-col gap-6">
        <div className="flex justify-between items-center px-2">
          <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-slate-400">Your Academies</h3>
          <button 
            onClick={() => setIsCreateModalOpen(true)}
            className="p-1 hover:bg-slate-100 dark:hover:bg-slate-900 rounded transition-colors text-primary-600"
          >
            <Plus size={16} strokeWidth={3} />
          </button>
        </div>
        
        <div className="flex-1 space-y-1 overflow-y-auto pr-2 custom-scrollbar">
          {isLoading ? (
            <div className="space-y-2 opacity-50">
              {[1, 2, 3].map(i => <div key={i} className="h-12 bg-slate-100 dark:bg-slate-900 rounded-lg animate-pulse" />)}
            </div>
          ) : courses?.map(course => (
            <button
              key={course.id}
              onClick={() => setSelectedCourseId(course.id)}
              className={clsx(
                "w-full text-left p-4 rounded-xl transition-all group border",
                selectedCourseId === course.id 
                  ? "bg-slate-900 text-white border-slate-900 dark:bg-white dark:text-slate-950 dark:border-white shadow-lg" 
                  : "bg-transparent border-transparent hover:border-slate-200 dark:hover:border-slate-800 text-slate-600"
              )}
            >
              <div className="flex items-center gap-3">
                <BookOpen size={16} strokeWidth={selectedCourseId === course.id ? 2.5 : 2} />
                <span className="text-sm font-bold truncate">{course.title}</span>
              </div>
              <div className="mt-3 flex items-center justify-between">
                <span className={clsx("text-[9px] font-black uppercase tracking-widest", 
                  selectedCourseId === course.id ? "text-slate-400" : "text-slate-400")}>
                  {course.status}
                </span>
                <span className="text-[10px] font-mono opacity-60">ID:{course.id.slice(-4)}</span>
              </div>
            </button>
          ))}
        </div>
      </div>

      {/* Editor: Structural Orchestration */}
      <div className="flex-1 border border-slate-200 dark:border-slate-800 rounded-xl bg-white dark:bg-slate-950 shadow-sm overflow-hidden flex flex-col">
        {selectedCourseId ? (
          <CourseEditor courseId={selectedCourseId} />
        ) : (
          <div className="flex-1 flex flex-col items-center justify-center text-center p-12 space-y-4">
            <div className="w-12 h-12 border-2 border-slate-100 dark:border-slate-900 rounded-full flex items-center justify-center text-slate-200">
               <BookOpen size={24} />
            </div>
            <div className="space-y-1">
              <h3 className="text-sm font-bold uppercase tracking-widest text-slate-400">No Academy Selected</h3>
              <p className="text-xs text-slate-500 font-medium">Select a resource to begin orchestration.</p>
            </div>
          </div>
        )}
      </div>

      <AnimatePresence>
        {isCreateModalOpen && (
          <CreateCourseModal onClose={() => setIsCreateModalOpen(false)} />
        )}
      </AnimatePresence>
    </div>
  );
};

const CourseEditor = ({ courseId }: { courseId: string }) => {
  const queryClient = useQueryClient();
  const [isAddSectionOpen, setIsAddSectionOpen] = useState(false);

  // DEEP FETCH: Get the full structure including sections and items
  const { data: course, isLoading } = useQuery({
    queryKey: ['course', courseId],
    queryFn: () => api.getCourse(courseId)
  });

  if (isLoading) return <div className="p-20 text-center text-slate-400 font-bold uppercase tracking-widest animate-pulse">Orchestrating Registry...</div>;
  if (!course) return <div className="p-20 text-center">Failed to load course details.</div>;

  return (
    <>
      <div className="p-8 border-b border-slate-100 dark:border-slate-800 flex justify-between items-end bg-slate-50/30 dark:bg-slate-900/10">
        <div className="space-y-1">
          <div className="flex items-center gap-2 text-[10px] font-bold text-primary-600 uppercase tracking-[0.2em]">
             <Layers size={12} />
             <span>Orchestrator Mode</span>
          </div>
          <h2 className="text-2xl font-black tracking-tight">{course.title}</h2>
          <p className="text-[10px] font-mono text-slate-400 uppercase tracking-widest">ARN: az:courses:global:course/{course.id}</p>
        </div>
        <div className="flex items-center gap-3">
           <button className="btn btn-secondary text-[10px] font-black uppercase tracking-widest px-6 h-9">Live Preview</button>
           <button className="btn btn-primary text-[10px] font-black uppercase tracking-widest px-6 h-9">Sync Changes</button>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-10 space-y-10 custom-scrollbar">
        {course.sections?.map((section: any) => (
          <SectionOrchestrator key={section.id} section={section} courseId={course.id} />
        ))}

        <button 
          onClick={() => setIsAddSectionOpen(true)}
          className="w-full py-6 border border-dashed border-slate-200 dark:border-slate-800 rounded-xl flex items-center justify-center gap-3 text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 hover:border-slate-400 dark:hover:border-slate-600 transition-all group"
        >
          <Plus size={18} className="group-hover:rotate-90 transition-transform duration-500" />
          <span className="text-[10px] font-black uppercase tracking-[0.3em]">Append Section</span>
        </button>
      </div>

      <AnimatePresence>
        {isAddSectionOpen && (
          <AddSectionModal courseId={course.id} onClose={() => setIsAddSectionOpen(false)} />
        )}
      </AnimatePresence>
    </>
  );
};

const SectionOrchestrator = ({ section, courseId }: { section: any, courseId: string }) => {
  const [isOpen, setIsOpen] = useState(true);
  const [showQuickAdd, setShowQuickAdd] = useState(false);
  const [modalType, setModalType] = useState<'video' | 'quiz' | null>(null);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between group">
        <div className="flex items-center gap-4">
          <button 
            onClick={() => setIsOpen(!isOpen)} 
            className={clsx("w-6 h-6 rounded flex items-center justify-center transition-all", isOpen ? "bg-slate-900 text-white dark:bg-white dark:text-slate-950" : "bg-slate-100 dark:bg-slate-900 text-slate-400")}
          >
            {isOpen ? <ChevronDown size={14} strokeWidth={3} /> : <ChevronRight size={14} strokeWidth={3} />}
          </button>
          <div className="space-y-0.5">
            <h4 className="text-sm font-black uppercase tracking-tight text-slate-900 dark:text-white">{section.title}</h4>
            <p className="text-[9px] font-mono text-slate-400 uppercase tracking-widest">az:section:{section.id}</p>
          </div>
        </div>
        
        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
          <button onClick={() => setShowQuickAdd(!showQuickAdd)} className="p-2 text-slate-400 hover:text-primary-600 hover:bg-primary-50 dark:hover:bg-primary-900/20 rounded-lg transition-all">
            <Plus size={16} />
          </button>
          <button className="p-2 text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-all">
            <Trash2 size={16} />
          </button>
        </div>
      </div>

      <AnimatePresence>
        {isOpen && (
          <motion.div 
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="pl-10 space-y-3"
          >
            {section.items?.map((item: any) => (
              <div key={item.id} className="flex items-center justify-between p-4 bg-slate-50/50 dark:bg-slate-900/20 rounded-lg border border-transparent hover:border-slate-200 dark:hover:border-slate-800 hover:bg-white dark:hover:bg-slate-950 transition-all group/item shadow-sm hover:shadow-md">
                <div className="flex items-center gap-5">
                  <div className={clsx(
                    "w-8 h-8 rounded flex items-center justify-center",
                    item.type === 'Lesson' ? "bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-white" : "bg-primary-600 text-white"
                  )}>
                    {item.type === 'Lesson' ? <Video size={14} /> : <FileText size={14} />}
                  </div>
                  <div>
                    <p className="text-sm font-bold text-slate-800 dark:text-slate-200">{item.title}</p>
                    <div className="flex items-center gap-3 mt-1">
                      <span className="text-[9px] font-mono text-slate-400 font-bold uppercase tracking-tighter">IDX:{item.bitIndex}</span>
                      <div className="w-1 h-1 bg-slate-300 dark:bg-slate-700 rounded-full" />
                      <span className="text-[9px] font-bold text-slate-500 uppercase tracking-widest">{item.type}</span>
                      {item.metadata?.Status && (
                        <>
                          <div className="w-1 h-1 bg-slate-300 dark:bg-slate-700 rounded-full" />
                          <div className="flex items-center gap-1.5">
                             <div className={clsx("w-1.5 h-1.5 rounded-full", item.metadata.Status === 'Ready' ? "bg-green-500" : "bg-amber-500")} />
                             <span className="text-[9px] font-black uppercase tracking-widest text-slate-400">{item.metadata.Status}</span>
                          </div>
                        </>
                      )}
                    </div>
                  </div>
                </div>
                <button className="p-1 text-slate-300 hover:text-slate-900 dark:hover:text-white transition-colors">
                  <MoreHorizontal size={16} />
                </button>
              </div>
            ))}

            {showQuickAdd && (
               <motion.div 
                 initial={{ opacity: 0, scale: 0.98 }} 
                 animate={{ opacity: 1, scale: 1 }}
                 className="grid grid-cols-2 gap-4 pt-4 border-t border-slate-100 dark:border-slate-900"
               >
                 <button onClick={() => setModalType('video')} className="flex items-center justify-between p-4 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-xl hover:border-slate-900 dark:hover:border-white transition-all group/btn text-left">
                    <div className="flex items-center gap-3">
                      <div className="p-2 bg-slate-100 dark:bg-slate-900 rounded-lg text-slate-600 group-hover/btn:bg-slate-900 group-hover/btn:text-white dark:group-hover/btn:bg-white dark:group-hover/btn:text-slate-950 transition-colors">
                        <Video size={16} />
                      </div>
                      <span className="text-[10px] font-black uppercase tracking-widest">Append Video</span>
                    </div>
                    <ArrowRight size={14} className="text-slate-300 group-hover/btn:text-slate-900 dark:group-hover/btn:text-white transition-all" />
                 </button>
                 <button onClick={() => setModalType('quiz')} className="flex items-center justify-between p-4 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-xl hover:border-slate-900 dark:hover:border-white transition-all group/btn text-left">
                    <div className="flex items-center gap-3">
                      <div className="p-2 bg-slate-100 dark:bg-slate-900 rounded-lg text-slate-600 group-hover/btn:bg-slate-900 group-hover/btn:text-white dark:group-hover/btn:bg-white dark:group-hover/btn:text-slate-950 transition-colors">
                        <FileText size={16} />
                      </div>
                      <span className="text-[10px] font-black uppercase tracking-widest">Inject Assessment</span>
                    </div>
                    <ArrowRight size={14} className="text-slate-300 group-hover/btn:text-slate-900 dark:group-hover/btn:text-white transition-all" />
                 </button>
               </motion.div>
            )}
          </motion.div>
        )}
      </AnimatePresence>

      <AnimatePresence>
        {modalType === 'video' && (
          <AddLessonModal courseId={courseId} sectionId={section.id} onClose={() => { setModalType(null); setShowQuickAdd(false); }} />
        )}
        {modalType === 'quiz' && (
          <AddAssessmentModal courseId={courseId} sectionId={section.id} onClose={() => { setModalType(null); setShowQuickAdd(false); }} />
        )}
      </AnimatePresence>
    </div>
  );
};

const AddLessonModal = ({ courseId, sectionId, onClose }: any) => {
  const queryClient = useQueryClient();
  const [title, setTitle] = useState('');
  const { data: videos } = useQuery({ queryKey: ['videos'], queryFn: () => api.getVideos() });
  const [selectedVideoId, setSelectedVideoId] = useState('');

  const addMutation = useMutation({
    mutationFn: () => api.addLesson(courseId, sectionId, { title, videoId: selectedVideoId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['course', courseId] });
      onClose();
    }
  });

  return (
    <div className="fixed inset-0 z-[120] flex items-center justify-center p-6 bg-slate-950/40 backdrop-blur-md">
      <motion.div initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }} className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-2xl w-full max-w-md p-8 space-y-6">
        <div className="flex justify-between items-center">
           <h3 className="text-lg font-bold">Append Video</h3>
           <button onClick={onClose}><X size={20} className="text-slate-400" /></button>
        </div>
        <div className="space-y-4">
          <div className="space-y-2">
            <label className="text-[10px] font-bold uppercase text-slate-400">Lesson Title</label>
            <input type="text" placeholder="e.g. Introduction to Force" value={title} onChange={e => setTitle(e.target.value)} />
          </div>
          <div className="space-y-2">
            <label className="text-[10px] font-bold uppercase text-slate-400">Select Asset</label>
            <select value={selectedVideoId} onChange={e => setSelectedVideoId(e.target.value)} className="w-full h-10 px-3 py-2 text-sm rounded-md border border-slate-200 dark:border-slate-800 bg-transparent">
              <option value="">Select a video...</option>
              {videos?.map(v => <option key={v.id} value={v.id}>{v.title}</option>)}
            </select>
          </div>
        </div>
        <button disabled={!title || !selectedVideoId || addMutation.isPending} onClick={() => addMutation.mutate()} className="w-full btn btn-primary py-3 uppercase tracking-widest text-[10px] font-bold">
           {addMutation.isPending ? 'Linking...' : 'Link Asset'}
        </button>
      </motion.div>
    </div>
  );
};

const AddAssessmentModal = ({ courseId, sectionId, onClose }: any) => {
  const queryClient = useQueryClient();
  const [title, setTitle] = useState('');
  const { data: quizzes } = useQuery({ queryKey: ['quizzes'], queryFn: () => api.getQuizzes() });
  const [selectedQuizId, setSelectedQuizId] = useState('');

  const addMutation = useMutation({
    mutationFn: () => {
      const q = quizzes?.find(x => x.id === selectedQuizId);
      return api.addAssessment(courseId, sectionId, { 
        title, 
        assessmentId: selectedQuizId,
        type: q?.type || 'MCQ',
        passingScore: q?.passingScore || 70,
        description: q?.description || ''
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['course', courseId] });
      onClose();
    }
  });

  return (
    <div className="fixed inset-0 z-[120] flex items-center justify-center p-6 bg-slate-950/40 backdrop-blur-md">
      <motion.div initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }} className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-2xl w-full max-w-md p-8 space-y-6">
        <div className="flex justify-between items-center">
           <h3 className="text-lg font-bold">Inject Assessment</h3>
           <button onClick={onClose}><X size={20} className="text-slate-400" /></button>
        </div>
        <div className="space-y-4">
          <div className="space-y-2">
            <label className="text-[10px] font-bold uppercase text-slate-400">Display Title</label>
            <input type="text" placeholder="e.g. Chapter 1 Final Exam" value={title} onChange={e => setTitle(e.target.value)} />
          </div>
          <div className="space-y-2">
            <label className="text-[10px] font-bold uppercase text-slate-400">Select Template</label>
            <select value={selectedQuizId} onChange={e => setSelectedQuizId(e.target.value)} className="w-full h-10 px-3 py-2 text-sm rounded-md border border-slate-200 dark:border-slate-800 bg-transparent">
              <option value="">Select a quiz...</option>
              {quizzes?.map(q => <option key={q.id} value={q.id}>{q.title}</option>)}
            </select>
          </div>
        </div>
        <button disabled={!title || !selectedQuizId || addMutation.isPending} onClick={() => addMutation.mutate()} className="w-full btn btn-primary py-3 uppercase tracking-widest text-[10px] font-bold">
           {addMutation.isPending ? 'Provisioning...' : 'Inject Quiz'}
        </button>
      </motion.div>
    </div>
  );
};

const AddSectionModal = ({ courseId, onClose }: { courseId: string, onClose: () => void }) => {
  const queryClient = useQueryClient();
  const [title, setTitle] = useState('');

  const addMutation = useMutation({
    mutationFn: () => api.addSection(courseId, title),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['courses'] });
      queryClient.invalidateQueries({ queryKey: ['course', courseId] });
      onClose();
    }
  });

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center p-6 bg-slate-950/20 dark:bg-slate-950/40 backdrop-blur-md">
      <motion.div 
        initial={{ opacity: 0, scale: 0.98, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-xl w-full max-w-sm p-8 space-y-6"
      >
        <div className="space-y-1">
          <h3 className="text-lg font-bold tracking-tight">New Section</h3>
          <p className="text-xs text-slate-500 font-medium">Add a logical grouping to your curriculum.</p>
        </div>
        <div className="space-y-2">
          <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Section Title</label>
          <input 
            type="text" 
            autoFocus
            placeholder="e.g. Fundamental Principles" 
            value={title}
            onChange={e => setTitle(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && title && addMutation.mutate()}
          />
        </div>
        <div className="flex gap-3">
          <button onClick={onClose} className="flex-1 btn btn-secondary uppercase tracking-widest text-[9px] font-bold">Cancel</button>
          <button 
            disabled={!title || addMutation.isPending}
            onClick={() => addMutation.mutate()}
            className="flex-1 btn btn-primary uppercase tracking-widest text-[9px] font-bold"
          >
            {addMutation.isPending ? 'Adding...' : 'Add Section'}
          </button>
        </div>
      </motion.div>
    </div>
  );
};

const CreateCourseModal = ({ onClose }: { onClose: () => void }) => {
  const queryClient = useQueryClient();
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [subjectId, setSubjectId] = useState('');
  const [isSuccess, setIsSuccess] = useState(false);

  const { data: subjects } = useQuery({
    queryKey: ['subjects'],
    queryFn: () => (api as any).getSubjects()
  });

  const createMutation = useMutation({
    mutationFn: () => api.createCourse({ title, description, subjectId }),
    onSuccess: () => {
      setIsSuccess(true);
      queryClient.invalidateQueries({ queryKey: ['courses'] });
      setTimeout(onClose, 1500);
    }
  });

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-6 bg-white/40 dark:bg-slate-950/40 backdrop-blur-xl">
      <motion.div 
        initial={{ opacity: 0, scale: 0.98, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.98, y: 20 }}
        className="bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-xl shadow-2xl w-full max-w-md overflow-hidden"
      >
        <div className="p-8 space-y-8">
          {!isSuccess ? (
            <>
              <div className="space-y-1">
                <h3 className="text-xl font-bold tracking-tight">Initialize Academy</h3>
                <p className="text-sm text-slate-500 font-medium">Create a new course registry in the orchestrator.</p>
              </div>

              <div className="space-y-5">
                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Academy Title</label>
                  <input 
                    type="text" 
                    placeholder="e.g. Theoretical Astrophysics" 
                    value={title}
                    onChange={e => setTitle(e.target.value)}
                  />
                </div>
                
                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Educational Subject</label>
                  <select 
                    value={subjectId}
                    onChange={e => setSubjectId(e.target.value)}
                    className="w-full h-10 px-3 py-2 text-sm rounded-md border border-slate-200 dark:border-slate-800 bg-transparent outline-none focus:ring-2 focus:ring-primary-500"
                  >
                    <option value="">Select a Subject...</option>
                    {subjects?.map(s => (
                      <option key={s.id} value={s.id}>{s.name}</option>
                    ))}
                  </select>
                </div>

                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Short Description</label>
                  <textarea 
                    className="w-full resize-none h-20 p-3 text-sm rounded-md border border-slate-200 dark:border-slate-800 bg-transparent"
                    placeholder="Brief scope of this academy..."
                    value={description}
                    onChange={e => setDescription(e.target.value)}
                  />
                </div>
              </div>

              <div className="flex gap-3 pt-2">
                <button onClick={onClose} className="flex-1 btn btn-secondary uppercase tracking-widest text-[10px] font-bold">Cancel</button>
                <button 
                  disabled={!title || !subjectId || createMutation.isPending}
                  onClick={() => createMutation.mutate()}
                  className="flex-1 btn btn-primary uppercase tracking-widest text-[10px] font-bold"
                >
                  {createMutation.isPending ? 'Provisioning...' : 'Create Registry'}
                </button>
              </div>
            </>
          ) : (
            <div className="py-10 text-center space-y-4 animate-in zoom-in-95 duration-500">
              <div className="w-12 h-12 bg-slate-900 dark:bg-white text-white dark:text-slate-950 rounded-full flex items-center justify-center mx-auto shadow-xl">
                <CheckCircle2 size={24} strokeWidth={3} />
              </div>
              <div className="space-y-1">
                <h3 className="text-lg font-bold tracking-tight uppercase tracking-[0.1em]">Registry Provisioned</h3>
                <p className="text-xs text-slate-500">Academy initialized in the course orchestrator.</p>
              </div>
            </div>
          )}
        </div>
      </motion.div>
    </div>
  );
};
