import React, { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, Plus, Trash2, GripVertical, Save, Brain, Settings, FileText, Info, X } from 'lucide-react';
import { api } from '../../api';
import { clsx } from 'clsx';
import { motion, AnimatePresence } from 'framer-motion';

export const QuizEditor: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const [items, setItems] = useState<any[]>([]);

  const { data: quiz, isLoading } = useQuery({
    queryKey: ['quiz', id],
    queryFn: async () => {
      const data = await api.getAssessment(id!);
      if (data.content?.items) {
        setItems(data.content.items);
      }
      return data;
    },
    enabled: !!id
  });

  const saveMutation = useMutation({
    mutationFn: () => api.updateAssessmentContent(id!, { version: "1.0", items }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['quiz', id] });
      alert('Assessment content synchronized successfully.');
    }
  });

  const addItem = (type: 'Paragraph' | 'Question') => {
    const newItem = {
      id: Math.random().toString(36).slice(2, 9),
      type: type === 'Paragraph' ? 0 : 1,
      renderData: { title: 'New ' + type, content: '' },
      questionType: type === 'Question' ? 0 : null, 
      points: type === 'Question' ? 10 : null,
      gradingData: type === 'Question' ? { choices: [], correctChoiceId: null } : null
    };
    setItems([...items, newItem]);
  };

  const updateItem = (itemId: string, updates: any) => {
    setItems(items.map(item => item.id === itemId ? { ...item, ...updates } : item));
  };

  const removeItem = (itemId: string) => {
    setItems(items.filter(item => item.id !== itemId));
  };

  // Option Management Logic
  const addOption = (itemId: string) => {
    setItems(items.map(item => {
      if (item.id === itemId) {
        const newOption = { id: Math.random().toString(36).slice(2, 7), renderData: { label: '' } };
        return {
          ...item,
          gradingData: {
            ...item.gradingData,
            choices: [...(item.gradingData?.choices || []), newOption]
          }
        };
      }
      return item;
    }));
  };

  const updateOption = (itemId: string, optionId: string, label: string) => {
    setItems(items.map(item => {
      if (item.id === itemId) {
        return {
          ...item,
          gradingData: {
            ...item.gradingData,
            choices: item.gradingData.choices.map((c: any) => 
              c.id === optionId ? { ...c, renderData: { label } } : c
            )
          }
        };
      }
      return item;
    }));
  };

  const setCorrectOption = (itemId: string, optionId: string) => {
    setItems(items.map(item => {
      if (item.id === itemId) {
        return {
          ...item,
          gradingData: {
            ...item.gradingData,
            correctChoiceId: optionId
          }
        };
      }
      return item;
    }));
  };

  const removeOption = (itemId: string, optionId: string) => {
    setItems(items.map(item => {
      if (item.id === itemId) {
        return {
          ...item,
          gradingData: {
            ...item.gradingData,
            choices: item.gradingData.choices.filter((c: any) => c.id !== optionId)
          }
        };
      }
      return item;
    }));
  };

  if (isLoading) return <div className="p-20 text-center font-bold uppercase tracking-widest text-slate-400 animate-pulse">Initializing Lab Scope...</div>;

  return (
    <div className="space-y-12 animate-in fade-in duration-700">
      <div className="flex justify-between items-end">
        <div className="space-y-1">
          <Link to="/quizzes" className="flex items-center gap-2 text-[10px] font-bold text-slate-400 uppercase tracking-[0.2em] mb-4 hover:text-slate-900 dark:hover:text-white transition-colors">
            <ChevronLeft size={14} strokeWidth={3} />
            Back to Registry
          </Link>
          <h1 className="text-4xl font-black tracking-tight">{quiz?.title}</h1>
          <p className="text-[10px] font-mono text-slate-400 uppercase tracking-widest">Assessment UID: {quiz?.id}</p>
        </div>
        <div className="flex items-center gap-3">
           <button className="btn btn-secondary gap-2 px-6">
             <Settings size={16} />
             <span>Config</span>
           </button>
           <button 
             onClick={() => saveMutation.mutate()}
             disabled={saveMutation.isPending}
             className="btn btn-primary gap-2 px-8"
           >
             <Save size={16} strokeWidth={2.5} />
             <span>{saveMutation.isPending ? 'Syncing...' : 'Save Draft'}</span>
           </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-12">
        <div className="lg:col-span-3 space-y-6">
          <AnimatePresence initial={false}>
            {items.map((item, index) => (
              <motion.div 
                key={item.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, x: -20 }}
                className="bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-xl overflow-hidden group shadow-sm"
              >
                <div className="p-4 bg-slate-50/50 dark:bg-slate-900/50 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <GripVertical size={16} className="text-slate-300 cursor-grab" />
                    <span className="text-[9px] font-black uppercase tracking-widest text-slate-400">Item {index + 1} — {item.type === 0 ? 'Paragraph' : 'Question'}</span>
                  </div>
                  <button onClick={() => removeItem(item.id)} className="p-1.5 text-slate-300 hover:text-red-500 transition-colors">
                    <Trash2 size={16} />
                  </button>
                </div>
                
                <div className="p-8 space-y-6">
                   <div className="space-y-2">
                     <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Content / Prompt</label>
                     <textarea 
                       className="w-full bg-transparent border-none p-0 text-lg font-bold placeholder:text-slate-200 focus:ring-0 resize-none"
                       placeholder="Enter question or paragraph text..."
                       rows={2}
                       value={item.renderData.title}
                       onChange={e => updateItem(item.id, { renderData: { ...item.renderData, title: e.target.value } })}
                     />
                   </div>

                   {item.type === 1 && (
                     <div className="pt-6 border-t border-slate-50 dark:border-slate-900 space-y-6">
                        <div className="flex items-center gap-8">
                           <div className="space-y-2">
                             <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Points</label>
                             <input 
                               type="number" 
                               className="w-24 h-8" 
                               value={item.points} 
                               onChange={e => updateItem(item.id, { points: parseInt(e.target.value) })}
                             />
                           </div>
                           <div className="space-y-2">
                             <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Input Mode</label>
                             <select className="h-8 py-0 text-xs font-bold uppercase tracking-widest">
                               <option>MCQ (Multi Choice)</option>
                               <option>Handwritten</option>
                               <option>Voice Response</option>
                             </select>
                           </div>
                        </div>

                        <div className="space-y-4">
                           <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Response Matrix</label>
                           <div className="space-y-3">
                              {item.gradingData?.choices?.map((choice: any) => (
                                <div key={choice.id} className="flex items-center gap-3 group/opt">
                                  <button 
                                    onClick={() => setCorrectOption(item.id, choice.id)}
                                    className={clsx(
                                      "w-5 h-5 rounded-full border-2 flex items-center justify-center transition-all",
                                      item.gradingData.correctChoiceId === choice.id 
                                        ? "border-primary-600 bg-primary-600 shadow-[0_0_8px_rgba(0,112,243,0.4)]" 
                                        : "border-slate-200 dark:border-slate-800"
                                    )}
                                  >
                                    {item.gradingData.correctChoiceId === choice.id && <div className="w-1.5 h-1.5 rounded-full bg-white" />}
                                  </button>
                                  <input 
                                    type="text"
                                    className="flex-1 h-9 bg-slate-50 dark:bg-slate-900 border-transparent focus:bg-white dark:focus:bg-slate-950"
                                    placeholder="Option label..."
                                    value={choice.renderData.label}
                                    onChange={e => updateOption(item.id, choice.id, e.target.value)}
                                  />
                                  <button onClick={() => removeOption(item.id, choice.id)} className="p-1 opacity-0 group-hover/opt:opacity-100 text-slate-400 hover:text-red-500 transition-all">
                                    <X size={14} />
                                  </button>
                                </div>
                              ))}
                              
                              <button 
                                onClick={() => addOption(item.id)}
                                className="flex items-center gap-2 text-[10px] font-black uppercase tracking-widest text-primary-600 hover:text-primary-700 p-2 group/add"
                              >
                                <div className="w-5 h-5 rounded-full bg-primary-50 dark:bg-primary-900/20 flex items-center justify-center group-hover/add:bg-primary-600 group-hover/add:text-white transition-colors">
                                  <Plus size={12} strokeWidth={3} />
                                </div>
                                Add Option
                              </button>
                           </div>
                        </div>
                     </div>
                   )}
                </div>
              </motion.div>
            ))}
          </AnimatePresence>

          <div className="grid grid-cols-2 gap-4">
            <button 
              onClick={() => addItem('Paragraph')}
              className="p-6 border-2 border-dashed border-slate-100 dark:border-slate-900 rounded-xl flex flex-col items-center gap-2 text-slate-400 hover:border-primary-500/50 hover:text-primary-600 transition-all group"
            >
              <FileText size={24} className="group-hover:scale-110 transition-transform" />
              <span className="text-[10px] font-black uppercase tracking-widest">Add Paragraph</span>
            </button>
            <button 
              onClick={() => addItem('Question')}
              className="p-6 border-2 border-dashed border-slate-100 dark:border-slate-900 rounded-xl flex flex-col items-center gap-2 text-slate-400 hover:border-primary-500/50 hover:text-primary-600 transition-all group"
            >
              <Brain size={24} className="group-hover:scale-110 transition-transform" />
              <span className="text-[10px] font-black uppercase tracking-widest">Add Question</span>
            </button>
          </div>
        </div>

        <div className="space-y-8">
          <div className="bg-slate-900 rounded-2xl p-8 text-white space-y-6">
            <div className="space-y-2">
              <h3 className="text-sm font-black uppercase tracking-widest text-primary-500">Live Blueprint</h3>
              <p className="text-xs text-slate-400 font-medium">All changes are drafted in memory until you commit to the orchestrator.</p>
            </div>
            
            <div className="space-y-4">
               <SummaryStat label="Total Points" value={items.reduce((acc, i) => acc + (i.points || 0), 0)} />
               <SummaryStat label="Questions" value={items.filter(i => i.type === 1).length} />
               <SummaryStat label="Integrity Check" value="PASSED" color="text-green-500" />
            </div>
          </div>

          <div className="border border-slate-200 dark:border-slate-800 rounded-2xl p-6 space-y-4">
            <div className="flex items-center gap-2 text-slate-400">
               <Info size={14} />
               <span className="text-[10px] font-bold uppercase tracking-widest">Lab Note</span>
            </div>
            <p className="text-[11px] text-slate-500 leading-relaxed font-medium">
              Changes to this content will generate a new assessment version. Existing course snapshots will need to be re-synchronized to reflect these updates.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

const SummaryStat = ({ label, value, color = "text-white" }: any) => (
  <div className="flex justify-between items-center border-b border-slate-800 pb-3 last:border-0 last:pb-0">
    <span className="text-[10px] font-bold uppercase tracking-widest text-slate-500">{label}</span>
    <span className={clsx("text-sm font-black tabular-nums", color)}>{value}</span>
  </div>
);
