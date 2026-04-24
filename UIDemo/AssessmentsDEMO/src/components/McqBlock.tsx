import { NodeViewWrapper } from '@tiptap/react';
import type { NodeViewProps } from '@tiptap/react';
import { Trash2, Plus, CheckCircle2, GripVertical, Settings2 } from 'lucide-react';
import { motion } from 'framer-motion';

export const McqBlock = ({ node, updateAttributes }: NodeViewProps) => {
  const { questionText, choices, correctChoiceId, points } = node.attrs;

  const addChoice = () => {
    const newId = `opt-${Date.now()}`;
    updateAttributes({
      choices: [...choices, { id: newId, text: 'New Option' }]
    });
  };

  const removeChoice = (id: string) => {
    updateAttributes({
      choices: choices.filter((c: any) => c.id !== id)
    });
  };

  const updateChoiceText = (id: string, text: string) => {
    updateAttributes({
      choices: choices.map((c: any) => c.id === id ? { ...c, text } : c)
    });
  };

  const setCorrectChoice = (id: string) => {
    updateAttributes({ correctChoiceId: id });
  };

  return (
    <NodeViewWrapper className="my-12 relative group/mcq">
      {/* Drag Handle & Quick Actions */}
      <div className="absolute -left-12 top-0 bottom-0 flex flex-col items-center py-4 gap-2 opacity-0 group-hover/mcq:opacity-100 transition-all duration-300">
        <button className="p-2 text-zinc-600 hover:text-zinc-400 cursor-grab active:cursor-grabbing">
          <GripVertical size={20} />
        </button>
        <button className="p-2 text-zinc-600 hover:text-zinc-400">
          <Settings2 size={18} />
        </button>
      </div>

      <motion.div 
        layout
        className="bg-zinc-900/80 border border-white/10 rounded-[2rem] p-10 shadow-2xl backdrop-blur-sm relative overflow-hidden"
      >
        {/* Progress gradient bar */}
        <div className="absolute top-0 left-0 right-0 h-[2px] bg-gradient-to-r from-indigo-500 via-purple-500 to-indigo-500 opacity-50" />

        <div className="flex justify-between items-center mb-10">
          <div className="flex items-center gap-4">
            <div className="w-8 h-8 rounded-lg bg-indigo-500/10 flex items-center justify-center text-indigo-400 text-xs font-black ring-1 ring-indigo-500/20">
              Q
            </div>
            <span className="text-[10px] font-black text-zinc-500 uppercase tracking-[0.2em]">Multiple Choice Question</span>
          </div>
          <div className="flex items-center gap-3 bg-white/5 px-4 py-2 rounded-xl border border-white/5 shadow-inner">
            <span className="text-[10px] font-bold text-zinc-500 uppercase tracking-widest">Weight</span>
            <input 
              type="number" 
              value={points} 
              onChange={(e) => updateAttributes({ points: parseInt(e.target.value) })}
              className="w-12 bg-transparent text-sm font-black text-indigo-400 outline-none text-center"
            />
          </div>
        </div>

        <input 
          type="text" 
          placeholder="What is the question?"
          value={questionText}
          onChange={(e) => updateAttributes({ questionText: e.target.value })}
          className="w-full bg-transparent text-3xl font-serif border-none mb-10 py-2 outline-none focus:ring-0 placeholder:text-zinc-800 transition-all"
        />

        <div className="grid gap-4">
          {choices.map((choice: any) => (
            <motion.div 
              key={choice.id} 
              layout
              className={`flex items-center gap-4 group/item p-2 rounded-2xl transition-all border ${correctChoiceId === choice.id ? 'bg-indigo-500/5 border-indigo-500/20' : 'bg-transparent border-transparent'}`}
            >
              <button 
                onClick={() => setCorrectChoice(choice.id)}
                className={`w-6 h-6 rounded-full border-2 transition-all flex items-center justify-center ${
                  correctChoiceId === choice.id 
                  ? 'border-indigo-500 bg-indigo-500 shadow-[0_0_15px_rgba(79,70,229,0.4)]' 
                  : 'border-zinc-700 hover:border-zinc-500'
                }`}
              >
                {correctChoiceId === choice.id && <CheckCircle2 size={14} className="text-white" />}
              </button>
              
              <input 
                type="text" 
                value={choice.text}
                onChange={(e) => updateChoiceText(choice.id, e.target.value)}
                className={`flex-1 bg-zinc-950/50 border border-white/5 rounded-xl px-4 py-3 text-sm outline-none focus:border-indigo-500/50 transition-all ring-1 ring-white/5 ${correctChoiceId === choice.id ? 'text-zinc-100 font-medium' : 'text-zinc-400'}`}
              />
              
              <button 
                onClick={() => removeChoice(choice.id)}
                className="p-3 text-zinc-600 hover:text-red-400 opacity-0 group-hover/item:opacity-100 transition-all active:scale-90"
              >
                <Trash2 size={16} />
              </button>
            </motion.div>
          ))}
        </div>

        <button 
          onClick={addChoice}
          className="mt-8 flex items-center gap-2 text-[10px] font-black uppercase tracking-widest text-indigo-400 hover:text-indigo-300 transition-all group/add pl-2"
        >
          <div className="p-1.5 rounded-lg bg-indigo-500/10 group-hover/add:bg-indigo-500/20 transition-all">
            <Plus size={14} /> 
          </div>
          Add Alternative
        </button>
      </motion.div>
    </NodeViewWrapper>
  );
};
