import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { FileText, Plus, Brain, Target, MoreHorizontal, ArrowRight, Info, CheckCircle2 } from 'lucide-react';
import { api } from '../../api';
import { Link } from 'react-router-dom';
import { clsx } from 'clsx';
import { motion, AnimatePresence } from 'framer-motion';

export const QuizDashboard: React.FC = () => {
  const { data: quizzes, isLoading } = useQuery({
    queryKey: ['quizzes'],
    queryFn: () => api.getQuizzes()
  });

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  return (
    <div className="space-y-12 animate-in fade-in duration-700">
      <div className="flex justify-between items-start">
        <div className="space-y-1">
          <h1 className="text-4xl font-black tracking-tight text-slate-900 dark:text-white">Assessment Architect</h1>
          <p className="text-slate-500 font-medium max-w-md">Design and deploy high-integrity quizzes for your academy.</p>
        </div>
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="btn btn-primary gap-2"
        >
          <Plus size={18} strokeWidth={3} />
          <span>New Assessment</span>
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
         {isLoading ? (
           <div className="col-span-full py-20 text-center text-slate-400 font-medium">Synchronizing assessments...</div>
         ) : quizzes?.map(quiz => (
           <Link 
             to={`/quizzes/${quiz.id}`}
             key={quiz.id}
             className="bg-white dark:bg-slate-950 p-6 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm hover:border-slate-400 dark:hover:border-slate-500 transition-all cursor-pointer group flex flex-col h-full"
           >
             <div className="flex justify-between items-start mb-6">
                <div className="w-10 h-10 bg-slate-900 dark:bg-white text-white dark:text-slate-950 rounded flex items-center justify-center">
                  <FileText size={20} />
                </div>
                <button className="p-1 text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 transition-colors">
                  <MoreHorizontal size={16} />
                </button>
             </div>
             
             <div className="flex-1 space-y-2 mb-8">
               <h3 className="text-lg font-bold tracking-tight">{quiz.title}</h3>
               <p className="text-xs text-slate-500 font-medium leading-relaxed line-clamp-2">{quiz.description}</p>
             </div>
             
             <div className="pt-6 border-t border-slate-100 dark:border-slate-900 flex items-center justify-between">
                <div className="flex items-center gap-4">
                  <div className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-slate-400">
                    <Brain size={12} className="text-primary-600" />
                    {quiz.type}
                  </div>
                  <div className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-slate-400">
                    <Target size={12} className="text-primary-600" />
                    {quiz.passingScore}% Pass
                  </div>
                </div>
                <ArrowRight size={14} className="text-slate-300 group-hover:text-primary-600 transition-colors" />
             </div>
           </Link>
         ))}

         <button 
           onClick={() => setIsCreateModalOpen(true)}
           className="border border-dashed border-slate-200 dark:border-slate-800 rounded-xl p-6 flex flex-col items-center justify-center text-slate-400 hover:border-slate-400 hover:text-slate-600 dark:hover:border-slate-600 dark:hover:text-slate-300 transition-all group min-h-[220px]"
         >
           <Plus size={24} className="mb-2 group-hover:scale-110 transition-transform" />
           <span className="text-[10px] font-bold uppercase tracking-[0.2em]">Add Assessment</span>
         </button>
      </div>

      <section className="bg-slate-50 dark:bg-slate-900/50 rounded-2xl p-10 border border-slate-200 dark:border-slate-800 flex flex-col md:flex-row items-center justify-between gap-10">
        <div className="space-y-4 max-w-xl">
          <div className="flex items-center gap-2 text-primary-600">
            <Brain size={20} strokeWidth={2.5} />
            <span className="text-[10px] font-bold uppercase tracking-[0.3em]">AI Integration</span>
          </div>
          <h2 className="text-3xl font-black tracking-tight">Generate from Syllabus</h2>
          <p className="text-slate-500 font-medium">Upload your course documents or video transcripts. Our LLM-orchestrated pipeline will generate multi-choice questions that map directly to your learning objectives.</p>
          <div className="pt-2">
            <button className="btn btn-primary gap-2">
              Open AI Lab
              <ArrowRight size={16} />
            </button>
          </div>
        </div>
        <div className="w-full md:w-64 h-64 border border-slate-200 dark:border-slate-800 rounded-xl bg-white dark:bg-slate-950 flex flex-col p-6 shadow-inner relative overflow-hidden group">
           <div className="space-y-3 opacity-40 group-hover:opacity-100 transition-opacity">
              <div className="h-2 bg-slate-100 dark:bg-slate-900 rounded w-3/4" />
              <div className="h-2 bg-slate-100 dark:bg-slate-900 rounded w-1/2" />
              <div className="h-2 bg-slate-100 dark:bg-slate-900 rounded w-5/6" />
           </div>
           <div className="absolute inset-0 flex items-center justify-center">
              <div className="w-12 h-12 rounded-full border-2 border-slate-100 dark:border-slate-800 flex items-center justify-center animate-pulse">
                 <div className="w-6 h-6 bg-slate-900 dark:bg-white rounded-full" />
              </div>
           </div>
           <div className="mt-auto space-y-2 text-[10px] font-mono text-slate-400">
              <p>ANALYZING_TRANSCRIPT...</p>
              <p>EXTRACTING_ENTITIES...</p>
           </div>
        </div>
      </section>

      <AnimatePresence>
        {isCreateModalOpen && (
          <CreateQuizModal onClose={() => setIsCreateModalOpen(false)} />
        )}
      </AnimatePresence>
    </div>
  );
};

const CreateQuizModal = ({ onClose }: { onClose: () => void }) => {
  const [step, setStep] = useState(1);
  const queryClient = useQueryClient();
  const [formData, setFormData] = useState({
    title: '',
    description: '',
    type: 'MCQ' as any,
    passingScore: 70
  });

  const createMutation = useMutation({
    mutationFn: (data: any) => api.createQuiz(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['quizzes'] });
      setStep(3);
      setTimeout(onClose, 1500);
    }
  });

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-6 bg-white/40 dark:bg-slate-950/40 backdrop-blur-xl">
      <motion.div 
        initial={{ opacity: 0, scale: 0.98 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.98 }}
        className="bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-xl shadow-2xl w-full max-w-2xl overflow-hidden flex h-[500px]"
      >
        {/* Left Side: Status */}
        <div className="w-48 border-r border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/30 p-8 hidden sm:flex flex-col">
          <div className="space-y-10">
            <StepItem number={1} label="Core Info" active={step === 1} done={step > 1} />
            <StepItem number={2} label="Logic" active={step === 2} done={step > 2} />
            <StepItem number={3} label="Finalize" active={step === 3} done={step > 3} />
          </div>
          <div className="mt-auto flex items-start gap-2 text-slate-400 italic">
            <Info size={14} className="mt-0.5 shrink-0" />
            <p className="text-[10px] leading-relaxed">Orchestration will automatically provision assessment UUIDs.</p>
          </div>
        </div>

        {/* Right Side: Form */}
        <div className="flex-1 p-10 flex flex-col">
          {step === 1 && (
            <div className="space-y-8 animate-in slide-in-from-right-4 duration-300">
              <div className="space-y-1">
                <h3 className="text-xl font-bold tracking-tight">Identity Definition</h3>
                <p className="text-sm text-slate-500 font-medium">Define how this assessment appears in the academy.</p>
              </div>
              <div className="space-y-6">
                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Quiz Title</label>
                  <input 
                    type="text" 
                    placeholder="e.g. Mechanical Foundations"
                    value={formData.title}
                    onChange={e => setFormData({...formData, title: e.target.value})}
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Summary Description</label>
                  <textarea 
                    className="w-full resize-none h-24 p-3 text-sm rounded-md border border-slate-200 dark:border-slate-800 bg-transparent"
                    placeholder="What will this test evaluate?"
                    value={formData.description}
                    onChange={e => setFormData({...formData, description: e.target.value})}
                  />
                </div>
              </div>
              <div className="flex justify-end gap-3 mt-auto">
                <button onClick={onClose} className="btn btn-secondary px-6 font-bold uppercase tracking-widest text-[10px]">Discard</button>
                <button 
                  disabled={!formData.title}
                  onClick={() => setStep(2)} 
                  className="btn btn-primary px-8 font-bold uppercase tracking-widest text-[10px]"
                >
                  Continue
                </button>
              </div>
            </div>
          )}

          {step === 2 && (
            <div className="space-y-8 animate-in slide-in-from-right-4 duration-300">
              <div className="space-y-1">
                <h3 className="text-xl font-bold tracking-tight">Scoring Logic</h3>
                <p className="text-sm text-slate-500 font-medium">Configure thresholds and question modalities.</p>
              </div>
              <div className="space-y-8">
                <div className="space-y-3">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Modal Type</label>
                  <div className="grid grid-cols-3 gap-3">
                    {['MCQ', 'Handwritten', 'Hybrid'].map(t => (
                      <button 
                        key={t}
                        onClick={() => setFormData({...formData, type: t as any})}
                        className={clsx(
                          "py-2 px-3 rounded text-[10px] font-black uppercase tracking-widest border transition-all",
                          formData.type === t 
                            ? "bg-slate-900 text-white border-slate-900 dark:bg-white dark:text-slate-950 dark:border-white" 
                            : "border-slate-200 dark:border-slate-800 text-slate-400 hover:border-slate-400"
                        )}
                      >
                        {t}
                      </button>
                    ))}
                  </div>
                </div>
                <div className="space-y-4">
                  <div className="flex justify-between items-end">
                    <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Success Threshold</label>
                    <span className="text-2xl font-black text-slate-900 dark:text-white">{formData.passingScore}%</span>
                  </div>
                  <input 
                    type="range" 
                    min="0" max="100" 
                    className="w-full accent-slate-900 dark:accent-white"
                    value={formData.passingScore}
                    onChange={e => setFormData({...formData, passingScore: parseInt(e.target.value)})}
                  />
                </div>
              </div>
              <div className="flex justify-between gap-3 mt-auto">
                <button onClick={() => setStep(1)} className="btn btn-ghost px-6 font-bold uppercase tracking-widest text-[10px]">Back</button>
                <button 
                  onClick={() => createMutation.mutate(formData)}
                  className="btn btn-primary px-10 font-bold uppercase tracking-widest text-[10px]"
                >
                  Deploy Quiz
                </button>
              </div>
            </div>
          )}

          {step === 3 && (
            <div className="flex-1 flex flex-col items-center justify-center text-center space-y-4 animate-in zoom-in-95 duration-500">
              <div className="w-12 h-12 bg-slate-900 dark:bg-white text-white dark:text-slate-950 rounded-full flex items-center justify-center shadow-xl">
                <CheckCircle2 size={24} strokeWidth={3} />
              </div>
              <h3 className="text-lg font-bold tracking-tight uppercase tracking-[0.1em]">Assessment Deployed</h3>
              <p className="text-xs text-slate-400 font-mono">ID: {Math.random().toString(36).slice(2, 10).toUpperCase()}</p>
            </div>
          )}
        </div>
      </motion.div>
    </div>
  );
};

const StepItem = ({ number, label, active, done }: any) => (
  <div className="flex items-center gap-3">
    <div className={clsx(
      "w-6 h-6 rounded flex items-center justify-center text-[10px] font-black border-2 transition-all",
      active ? "border-slate-900 bg-slate-900 text-white dark:border-white dark:bg-white dark:text-slate-950" : 
      done ? "border-slate-900 text-slate-900 dark:border-white dark:text-white" : "border-slate-200 dark:border-slate-800 text-slate-300"
    )}>
      {done ? "✓" : number}
    </div>
    <span className={clsx(
      "text-[10px] font-bold uppercase tracking-widest",
      active ? "text-slate-900 dark:text-white" : "text-slate-400"
    )}>{label}</span>
  </div>
);
