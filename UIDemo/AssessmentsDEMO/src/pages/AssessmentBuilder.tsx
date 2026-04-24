import { useEditor, EditorContent, Node, ReactNodeViewRenderer } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { McqBlock } from '../components/McqBlock';
import { 
  Type, 
  HelpCircle, 
  FileEdit, 
  Save, 
  ChevronLeft,
  Bold,
  Italic,
  List,
  Heading2,
  Undo2,
  Redo2,
  Sparkles,
  Command
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';

// Define the Custom MCQ Extension
const CustomMcqNode = Node.create({
  name: 'mcqBlock',
  group: 'block',
  atom: true,
  draggable: true,
  addAttributes() {
    return {
      questionText: { default: '' },
      choices: { default: [{ id: 'opt-1', text: 'Option 1' }] },
      correctChoiceId: { default: 'opt-1' },
      points: { default: 5 }
    };
  },
  parseHTML() { return [{ tag: 'mcq-block' }]; },
  renderHTML({ HTMLAttributes }) { return ['mcq-block', HTMLAttributes]; },
  addNodeView() {
    return ReactNodeViewRenderer(McqBlock);
  }
});

const AssessmentBuilder = () => {
  const navigate = useNavigate();

  const editor = useEditor({
    extensions: [
      StarterKit,
      CustomMcqNode
    ],
    content: `
      <h2 style="font-family: var(--font-serif); font-size: 2.5rem; margin-bottom: 1rem;">Physics Final Exam: Mechanics</h2>
      <p>This comprehensive evaluation covers Newton's laws, energy conservation, and rotational dynamics. Please ensure all free-body diagrams are clear.</p>
      <mcq-block></mcq-block>
    `,
    editorProps: {
      attributes: {
        class: 'prose prose-invert prose-zinc max-w-none focus:outline-none min-h-[700px] px-12 py-16 text-lg font-light',
      },
    },
  });

  const handleSave = () => {
    if (!editor) return;
    const json = editor.getJSON();
    console.log('Saving Assessment JSON:', json);
    alert('Assessment Blueprint Saved to Database.');
  };

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 flex flex-col w-full selection:bg-indigo-500/30">
      {/* Top Navigation */}
      <header className="h-16 border-b border-white/5 bg-zinc-900/50 backdrop-blur-xl flex items-center px-6 sticky top-0 z-[100]">
        <div className="flex-1 flex items-center gap-4">
          <button onClick={() => navigate('/')} className="p-2 hover:bg-white/5 rounded-xl transition-all text-zinc-400 hover:text-white">
            <ChevronLeft size={20} />
          </button>
          <div className="h-4 w-[1px] bg-white/10 mx-2" />
          <h1 className="text-sm font-bold tracking-widest uppercase text-zinc-500">Draft / Physics Final</h1>
        </div>
        
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2 px-3 py-1.5 bg-indigo-500/10 text-indigo-400 rounded-lg text-xs font-bold border border-indigo-500/20 mr-4">
            <Sparkles size={14} /> AI Suggestions Active
          </div>
          <button 
            onClick={handleSave}
            className="bg-indigo-600 hover:bg-indigo-500 text-white px-6 py-2 rounded-xl font-bold transition-all shadow-lg shadow-indigo-600/20 flex items-center gap-2 text-sm"
          >
            <Save size={16} /> Publish Exam
          </button>
        </div>
      </header>

      <main className="flex-1 flex overflow-hidden">
        {/* Refined Sidebar */}
        <aside className="w-80 border-r border-white/5 p-8 space-y-12 bg-zinc-900/20 overflow-y-auto">
          <div>
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-[10px] font-black text-zinc-500 uppercase tracking-[0.2em]">Components</h3>
              <Command size={14} className="text-zinc-600" />
            </div>
            <div className="grid gap-3">
              <button 
                onClick={() => editor?.chain().focus().setParagraph().run()}
                className="flex items-center gap-4 p-4 bg-zinc-900 border border-white/5 rounded-2xl hover:border-indigo-500/50 hover:bg-zinc-800/50 transition-all text-left group"
              >
                <div className="p-2 bg-white/5 rounded-lg text-zinc-400 group-hover:text-indigo-400 group-hover:bg-indigo-500/10 transition-all">
                  <Type size={18} />
                </div>
                <div>
                  <p className="text-sm font-bold">Standard Text</p>
                  <p className="text-[10px] text-zinc-500">Instructional paragraphs</p>
                </div>
              </button>
              
              <button 
                onClick={() => editor?.chain().focus().insertContent({ type: 'mcqBlock' }).run()}
                className="flex items-center gap-4 p-4 bg-zinc-900 border border-white/5 rounded-2xl hover:border-purple-500/50 hover:bg-zinc-800/50 transition-all text-left group"
              >
                <div className="p-2 bg-white/5 rounded-lg text-zinc-400 group-hover:text-purple-400 group-hover:bg-purple-500/10 transition-all">
                  <HelpCircle size={18} />
                </div>
                <div>
                  <p className="text-sm font-bold">MCQ Question</p>
                  <p className="text-[10px] text-zinc-500">Automated grading</p>
                </div>
              </button>

              <button className="flex items-center gap-4 p-4 bg-zinc-900/50 border border-white/5 rounded-2xl opacity-50 cursor-not-allowed text-left grayscale">
                <div className="p-2 bg-white/5 rounded-lg text-zinc-400">
                  <FileEdit size={18} />
                </div>
                <div>
                  <p className="text-sm font-bold">Handwritten</p>
                  <p className="text-[10px] text-zinc-500">AI-assisted review</p>
                </div>
              </button>
            </div>
          </div>

          <div className="pt-8 border-t border-white/5">
            <h3 className="text-[10px] font-black text-zinc-500 uppercase tracking-[0.2em] mb-6">Exam Meta</h3>
            <div className="space-y-6">
              <div className="space-y-3">
                <label className="text-[10px] font-bold text-zinc-400 uppercase tracking-widest">Time Limit (Mins)</label>
                <input type="number" defaultValue={60} className="w-full bg-zinc-900 border border-white/5 rounded-xl p-3 text-sm focus:border-indigo-500 outline-none ring-1 ring-white/5" />
              </div>
              <div className="space-y-3">
                <label className="text-[10px] font-bold text-zinc-400 uppercase tracking-widest">Passing Criteria</label>
                <div className="flex items-center gap-3">
                  <input type="number" defaultValue={50} className="flex-1 bg-zinc-900 border border-white/5 rounded-xl p-3 text-sm focus:border-indigo-500 outline-none ring-1 ring-white/5 text-center font-bold" />
                  <span className="text-lg font-serif text-zinc-600">%</span>
                </div>
              </div>
            </div>
          </div>
        </aside>

        {/* Editor Workspace */}
        <section className="flex-1 bg-zinc-950 overflow-y-auto custom-scrollbar flex flex-col items-center">
          <div className="max-w-[850px] w-full my-20">
            {/* Contextual Floating Toolbar */}
            <AnimatePresence>
              {editor && (
                <motion.div 
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  className="mb-8 flex items-center justify-center"
                >
                  <div className="bg-zinc-900/80 backdrop-blur-2xl border border-white/10 p-1.5 rounded-2xl shadow-2xl flex items-center gap-1 ring-1 ring-white/5">
                    <ToolbarButton icon={<Undo2 size={16} />} onClick={() => editor.chain().focus().undo().run()} />
                    <ToolbarButton icon={<Redo2 size={16} />} onClick={() => editor.chain().focus().redo().run()} />
                    <div className="w-[1px] h-4 bg-white/10 mx-1" />
                    <ToolbarButton 
                      active={editor.isActive('heading', { level: 2 })} 
                      icon={<Heading2 size={18} />} 
                      onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()} 
                    />
                    <ToolbarButton 
                      active={editor.isActive('bold')} 
                      icon={<Bold size={16} />} 
                      onClick={() => editor.chain().focus().toggleBold().run()} 
                    />
                    <ToolbarButton 
                      active={editor.isActive('italic')} 
                      icon={<Italic size={16} />} 
                      onClick={() => editor.chain().focus().toggleItalic().run()} 
                    />
                    <div className="w-[1px] h-4 bg-white/10 mx-1" />
                    <ToolbarButton 
                      active={editor.isActive('bulletList')} 
                      icon={<List size={18} />} 
                      onClick={() => editor.chain().focus().toggleBulletList().run()} 
                    />
                  </div>
                </motion.div>
              )}
            </AnimatePresence>

            <div className="bg-zinc-900/40 border border-white/5 rounded-[2.5rem] shadow-[0_40px_100px_rgba(0,0,0,0.4)] relative ring-1 ring-white/10 group/canvas overflow-hidden">
               {/* Paper texture overlay */}
               <div className="absolute inset-0 opacity-[0.03] pointer-events-none bg-[url('https://www.transparenttextures.com/patterns/carbon-fibre.png')]" />
               
               <EditorContent editor={editor} />
            </div>
          </div>
        </section>
      </main>
    </div>
  );
};

const ToolbarButton = ({ icon, onClick, active = false }: { icon: React.ReactNode, onClick: () => void, active?: boolean }) => (
  <button 
    onClick={onClick}
    className={`p-2.5 rounded-xl transition-all flex items-center justify-center ${active ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-600/30' : 'text-zinc-400 hover:bg-white/5 hover:text-white'}`}
  >
    {icon}
  </button>
);

export default AssessmentBuilder;
