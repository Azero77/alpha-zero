import React from 'react';
import { NodeViewWrapper, NodeViewProps } from '@tiptap/react';
import { Trash2, Plus, CheckCircle2 } from 'lucide-react';

export const McqBlock: React.FC<NodeViewProps> = ({ node, updateAttributes }) => {
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
    <NodeViewWrapper className="mcq-block bg-slate-900 border border-slate-700 rounded-lg p-6 my-4 shadow-xl">
      <div className="flex justify-between items-center mb-4">
        <span className="text-xs font-bold text-blue-400 uppercase tracking-widest">MCQ Question</span>
        <div className="flex items-center gap-2">
          <label className="text-xs text-slate-400">Points:</label>
          <input 
            type="number" 
            value={points} 
            onChange={(e) => updateAttributes({ points: parseInt(e.target.value) })}
            className="w-16 bg-slate-800 border border-slate-600 rounded px-2 py-1 text-sm outline-none focus:border-blue-500"
          />
        </div>
      </div>

      <input 
        type="text" 
        placeholder="Enter your question here..."
        value={questionText}
        onChange={(e) => updateAttributes({ questionText: e.target.value })}
        className="w-full bg-transparent text-xl font-medium border-b border-slate-700 mb-6 py-2 outline-none focus:border-blue-500 transition-colors"
      />

      <div className="space-y-3">
        {choices.map((choice: any) => (
          <div key={choice.id} className="flex items-center gap-3 group">
            <button 
              onClick={() => setCorrectChoice(choice.id)}
              className={`p-1 rounded-full transition-colors ${correctChoiceId === choice.id ? 'text-green-500 bg-green-500/10' : 'text-slate-600 hover:text-slate-400'}`}
            >
              <CheckCircle2 size={20} />
            </button>
            <input 
              type="text" 
              value={choice.text}
              onChange={(e) => updateChoiceText(choice.id, e.target.value)}
              className="flex-1 bg-slate-800 border border-slate-700 rounded px-3 py-2 text-sm outline-none focus:border-blue-500"
            />
            <button 
              onClick={() => removeChoice(choice.id)}
              className="p-2 text-slate-500 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-all"
            >
              <Trash2 size={16} />
            </button>
          </div>
        ))}
      </div>

      <button 
        onClick={addChoice}
        className="mt-6 flex items-center gap-2 text-sm text-blue-400 hover:text-blue-300 transition-colors font-medium"
      >
        <Plus size={16} /> Add Option
      </button>
    </NodeViewWrapper>
  );
};
