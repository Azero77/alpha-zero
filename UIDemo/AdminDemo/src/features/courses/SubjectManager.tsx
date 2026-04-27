import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Layers, Plus, Search, MoreHorizontal, CheckCircle2, Info, BookMarked } from 'lucide-react';
import { api } from '../../api';
import { clsx } from 'clsx';
import { motion, AnimatePresence } from 'framer-motion';

export const SubjectManager: React.FC = () => {
  const queryClient = useQueryClient();
  const [isModalOpen, setIsModalOpen] = useState(false);

  const { data: subjects, isLoading } = useQuery({
    queryKey: ['subjects'],
    queryFn: () => (api as any).getSubjects()
  });

  return (
    <div className="space-y-12 animate-in fade-in duration-700">
      <div className="flex justify-between items-start">
        <div className="space-y-1">
          <h1 className="text-4xl font-black tracking-tight text-slate-900 dark:text-white">Taxonomy Manager</h1>
          <p className="text-slate-500 font-medium max-w-md">Define the educational foundation of your academy.</p>
        </div>
        <button 
          onClick={() => setIsModalOpen(true)}
          className="btn btn-primary gap-2"
        >
          <Plus size={18} strokeWidth={3} />
          <span>New Subject</span>
        </button>
      </div>

      <section className="space-y-4">
        <div className="flex items-center justify-between px-2">
          <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-slate-400">Educational Categories</h3>
          <div className="relative group">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={14} />
            <input 
              type="text" 
              placeholder="Search taxonomy..." 
              className="h-8 pl-9 pr-4 w-48 bg-transparent border-slate-200 dark:border-slate-800"
            />
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {isLoading ? (
            [1, 2, 3].map(i => <div key={i} className="h-40 bg-slate-50 dark:bg-slate-900/50 rounded-xl border border-slate-200 dark:border-slate-800 animate-pulse" />)
          ) : subjects?.map((subject: any) => (
            <div 
              key={subject.id}
              className="bg-white dark:bg-slate-950 p-6 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm hover:border-slate-400 dark:hover:border-slate-500 transition-all group"
            >
              <div className="flex justify-between items-start mb-6">
                <div className="w-10 h-10 bg-slate-100 dark:bg-slate-900 text-slate-600 dark:text-slate-400 rounded flex items-center justify-center group-hover:bg-slate-900 dark:group-hover:bg-white group-hover:text-white dark:group-hover:text-slate-950 transition-colors">
                  <BookMarked size={20} />
                </div>
                <button className="p-1 text-slate-300 hover:text-slate-900 dark:hover:text-white transition-colors">
                  <MoreHorizontal size={16} />
                </button>
              </div>
              <div className="space-y-1">
                <h4 className="font-bold text-slate-900 dark:text-white">{subject.name}</h4>
                <p className="text-xs text-slate-500 font-medium leading-relaxed line-clamp-2">{subject.description || "No classification details provided."}</p>
              </div>
              <div className="mt-6 pt-4 border-t border-slate-50 dark:border-slate-900 flex justify-between items-center">
                 <span className="text-[9px] font-mono text-slate-400 uppercase tracking-widest">ID:{subject.id.slice(0, 8)}</span>
                 <div className="flex items-center gap-1.5">
                   <div className="w-1 h-1 rounded-full bg-green-500" />
                   <span className="text-[9px] font-bold text-slate-400 uppercase tracking-widest">Active</span>
                 </div>
              </div>
            </div>
          ))}
        </div>
      </section>

      <AnimatePresence>
        {isModalOpen && (
          <CreateSubjectModal onClose={() => setIsModalOpen(false)} />
        )}
      </AnimatePresence>
    </div>
  );
};

const CreateSubjectModal = ({ onClose }: { onClose: () => void }) => {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isSuccess, setIsSuccess] = useState(false);

  const createMutation = useMutation({
    mutationFn: () => (api as any).createSubject({ name, description }),
    onSuccess: () => {
      setIsSuccess(true);
      queryClient.invalidateQueries({ queryKey: ['subjects'] });
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
        <div className="p-10 space-y-8">
          {!isSuccess ? (
            <>
              <div className="space-y-1">
                <h3 className="text-xl font-bold tracking-tight text-slate-900 dark:text-white">Provision Subject</h3>
                <p className="text-sm text-slate-500 font-medium">Add a new educational category to the registry.</p>
              </div>

              <div className="space-y-6">
                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Subject Name</label>
                  <input 
                    type="text" 
                    placeholder="e.g. Quantum Mechanics" 
                    value={name}
                    onChange={e => setName(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-slate-400">Class Description</label>
                  <textarea 
                    className="w-full resize-none h-24 p-3 text-sm rounded-md border border-slate-200 dark:border-slate-800 bg-transparent"
                    placeholder="Detailed classification of this subject area..."
                    value={description}
                    onChange={e => setDescription(e.target.value)}
                  />
                </div>
              </div>

              <div className="flex gap-3 pt-2">
                <button onClick={onClose} className="flex-1 btn btn-secondary uppercase tracking-widest text-[10px] font-bold">Discard</button>
                <button 
                  disabled={!name || createMutation.isPending}
                  onClick={() => createMutation.mutate()}
                  className="flex-1 btn btn-primary uppercase tracking-widest text-[10px] font-bold"
                >
                  {createMutation.isPending ? 'Provisioning...' : 'Add Subject'}
                </button>
              </div>
            </>
          ) : (
            <div className="py-10 text-center space-y-4 animate-in zoom-in-95 duration-500">
              <div className="w-12 h-12 bg-slate-900 dark:bg-white text-white dark:text-slate-950 rounded-full flex items-center justify-center mx-auto shadow-xl">
                <CheckCircle2 size={24} strokeWidth={3} />
              </div>
              <div className="space-y-1">
                <h3 className="text-lg font-bold tracking-tight uppercase tracking-[0.1em]">Subject Provisioned</h3>
                <p className="text-xs text-slate-500">New classification added to the global registry.</p>
              </div>
            </div>
          )}
        </div>
      </motion.div>
    </div>
  );
};
