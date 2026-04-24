import React from 'react';
import { useEditor, EditorContent, Node } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { ReactNodeViewRenderer } from '@tiptap/react';
import { McqBlock } from '../components/McqBlock';
import { 
  Type, 
  HelpCircle, 
  FileEdit, 
  Save, 
  Eye, 
  ChevronLeft,
  LayoutGrid
} from 'lucide-react';
import { useAssessmentStore } from '../store/useAssessmentStore';
import { useNavigate } from 'react-router-dom';

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
  const { addAssessment } = useAssessmentStore();

  const editor = useEditor({
    extensions: [
      StarterKit,
      CustomMcqNode
    ],
    content: '<p>Start building your professional exam here...</p>',
    editorProps: {
      attributes: {
        class: 'prose prose-invert max-w-none focus:outline-none min-h-[500px] p-8',
      },
    },
  });

  const handleSave = () => {
    if (!editor) return;
    const json = editor.getJSON();
    console.log('Saving Assessment JSON:', json);
    // In a real app, this would be the POST /assessments call
    alert('Assessment Saved Successfully! (Check console for JSON)');
  };

  return (
    <div className="min-h-screen bg-black text-slate-100 flex flex-col w-full">
      {/* Header */}
      <header className="border-b border-slate-800 p-4 bg-slate-900/50 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-6xl mx-auto flex items-center justify-between">
          <div className="flex items-center gap-4">
            <button onClick={() => navigate('/')} className="p-2 hover:bg-slate-800 rounded-lg transition-colors">
              <ChevronLeft />
            </button>
            <h1 className="text-xl font-bold bg-gradient-to-r from-blue-400 to-indigo-400 bg-clip-text text-transparent">
              Assessment Builder
            </h1>
          </div>
          <div className="flex items-center gap-3">
            <button className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-slate-400 hover:text-white transition-colors">
              <Eye size={18} /> Preview
            </button>
            <button 
              onClick={handleSave}
              className="flex items-center gap-2 bg-blue-600 hover:bg-blue-500 px-6 py-2 rounded-lg font-bold transition-all shadow-lg shadow-blue-600/20"
            >
              <Save size={18} /> Save Exam
            </button>
          </div>
        </div>
      </header>

      <main className="flex-1 flex max-w-[1400px] mx-auto w-full">
        {/* Sidebar Tools */}
        <aside className="w-64 border-r border-slate-800 p-6 space-y-8 sticky top-20 h-[calc(100vh-80px)]">
          <div>
            <h3 className="text-xs font-bold text-slate-500 uppercase tracking-widest mb-4">Content Blocks</h3>
            <div className="space-y-2">
              <button 
                onClick={() => editor?.chain().focus().setParagraph().run()}
                className="w-full flex items-center gap-3 p-3 bg-slate-900 hover:bg-slate-800 border border-slate-800 rounded-lg transition-all text-sm group"
              >
                <Type size={18} className="text-blue-400 group-hover:scale-110 transition-transform" /> 
                Text Block
              </button>
              <button 
                onClick={() => editor?.chain().focus().insertContent({ type: 'mcqBlock' }).run()}
                className="w-full flex items-center gap-3 p-3 bg-slate-900 hover:bg-slate-800 border border-slate-800 rounded-lg transition-all text-sm group"
              >
                <HelpCircle size={18} className="text-purple-400 group-hover:scale-110 transition-transform" /> 
                MCQ Question
              </button>
              <button className="w-full flex items-center gap-3 p-3 bg-slate-900/50 cursor-not-allowed border border-slate-800 rounded-lg text-sm text-slate-500 opacity-60">
                <FileEdit size={18} /> Handwritten
              </button>
            </div>
          </div>

          <div>
            <h3 className="text-xs font-bold text-slate-500 uppercase tracking-widest mb-4">Exam Settings</h3>
            <div className="space-y-4">
              <div className="space-y-2">
                <label className="text-xs text-slate-400">Passing Score (%)</label>
                <input type="number" defaultValue={50} className="w-full bg-slate-900 border border-slate-800 rounded px-3 py-2 text-sm outline-none focus:border-blue-500" />
              </div>
              <div className="flex items-center gap-2">
                <input type="checkbox" className="rounded bg-slate-900 border-slate-800" />
                <label className="text-sm text-slate-400">Randomize Questions</label>
              </div>
            </div>
          </div>
        </aside>

        {/* Editor Area */}
        <section className="flex-1 bg-slate-950 min-h-screen">
          <div className="max-w-4xl mx-auto py-12 px-8">
            <div className="bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl overflow-hidden ring-1 ring-white/5">
              <div className="bg-slate-800/50 p-3 border-b border-slate-800 flex gap-2">
                {/* Basic Editor Controls */}
                <button 
                  onClick={() => editor?.chain().focus().toggleBold().run()}
                  className={`p-2 rounded hover:bg-slate-700 transition-colors ${editor?.isActive('bold') ? 'text-blue-400 bg-slate-700' : 'text-slate-400'}`}
                >
                  <span className="font-bold">B</span>
                </button>
                <button 
                  onClick={() => editor?.chain().focus().toggleHeading({ level: 2 }).run()}
                  className={`p-2 rounded hover:bg-slate-700 transition-colors ${editor?.isActive('heading') ? 'text-blue-400 bg-slate-700' : 'text-slate-400'}`}
                >
                  <span className="font-bold">H2</span>
                </button>
                <div className="w-[1px] bg-slate-700 mx-2"></div>
                <button 
                  onClick={() => editor?.chain().focus().toggleBulletList().run()}
                  className={`p-2 rounded hover:bg-slate-700 transition-colors ${editor?.isActive('bulletList') ? 'text-blue-400 bg-slate-700' : 'text-slate-400'}`}
                >
                  <LayoutGrid size={18} />
                </button>
              </div>
              <EditorContent editor={editor} />
            </div>
          </div>
        </section>
      </main>
    </div>
  );
};

export default AssessmentBuilder;
