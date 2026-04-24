import { useState } from 'react';
import { 
  ChevronLeft, 
  CheckCircle, 
  MessageSquare,
  Sparkles,
  Award,
  Zap,
  Target,
  FileSearch,
  ScanEye
} from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAssessmentStore } from '../store/useAssessmentStore';
import { motion } from 'framer-motion';

const ManualGrading = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { submissions, updateSubmission, assessments } = useAssessmentStore();
  
  const submission = submissions.find(s => s.id === id) || {
    id: 'sub-1',
    assessmentId: 'assessment-1',
    studentId: 'Ali Hassan',
    status: 'UnderReview',
    submittedAt: '2026-04-23T10:00:00Z',
    responses: { 
      answers: { 
        'q-1': 'opt-1', 
        'q-2': 's3://tenant-1/Ali_Ali_Photo.jpg' 
      } 
    }
  } as any;

  const assessment = assessments.find(a => a.id === submission.assessmentId);
  const handwrittenQuestion = assessment?.versions[0].content.items.find(i => i.questionType === 'Handwritten');

  const [score, setScore] = useState<number>(18);
  const [feedback, setFeedback] = useState<string>('');

  const handleFinalize = () => {
    updateSubmission(submission.id, { 
      status: 'Graded', 
      totalScore: score + 10,
      gradedAt: new Date().toISOString()
    });
    alert('Evaluation successfully committed to the blockchain/database.');
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 flex flex-col w-full selection:bg-indigo-500/30 font-sans overflow-hidden">
      {/* Immersive Grading Header */}
      <header className="bg-zinc-900 border-b border-white/5 p-6 z-50 shadow-2xl relative">
        <div className="max-w-[1800px] mx-auto flex items-center justify-between">
          <div className="flex items-center gap-8">
            <button onClick={() => navigate('/')} className="p-3 hover:bg-white/5 rounded-2xl transition-all text-zinc-500 hover:text-white border border-transparent hover:border-white/10">
              <ChevronLeft size={24} />
            </button>
            <div>
              <div className="flex items-center gap-3 mb-1">
                <h1 className="font-serif text-3xl tracking-tight leading-none">{assessment?.title}</h1>
                <span className="text-[10px] font-black px-2 py-0.5 bg-indigo-500/10 text-indigo-400 rounded-md border border-indigo-500/20 uppercase tracking-widest">
                  Manual Review
                </span>
              </div>
              <div className="flex items-center gap-6">
                <div className="flex items-center gap-2">
                   <div className="w-6 h-6 rounded-full bg-zinc-800 border border-white/10 flex items-center justify-center text-[10px] font-bold">AH</div>
                   <span className="text-sm font-medium text-zinc-400">{submission.studentId}</span>
                </div>
                <div className="w-1.5 h-1.5 rounded-full bg-zinc-800" />
                <span className="text-xs text-zinc-600 font-mono">ID: {submission.id}</span>
              </div>
            </div>
          </div>
          <button 
            onClick={handleFinalize}
            className="bg-white hover:bg-zinc-200 text-zinc-950 px-10 py-3.5 rounded-2xl font-black uppercase tracking-[0.1em] text-xs transition-all shadow-[0_20px_50px_rgba(255,255,255,0.1)] hover:scale-105 active:scale-95 flex items-center gap-3"
          >
            <CheckCircle size={18} /> Finalize Grade
          </button>
        </div>
      </header>

      <main className="flex-1 flex w-full overflow-hidden">
        {/* Left: Interactive Canvas */}
        <div className="flex-1 bg-black overflow-y-auto custom-scrollbar p-16 relative">
          {/* Subtle grid background */}
          <div className="absolute inset-0 opacity-[0.15] pointer-events-none bg-[radial-gradient(#27272a_1px,transparent_1px)] [background-size:24px_24px]" />
          
          <div className="max-w-4xl mx-auto space-y-20 relative">
            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              className="space-y-6"
            >
              <div className="flex items-center gap-3">
                <div className="p-2 bg-white/5 rounded-xl text-zinc-500">
                  <FileSearch size={18} />
                </div>
                <h2 className="text-[10px] font-black text-zinc-500 uppercase tracking-[0.3em]">Evaluation Prompt</h2>
              </div>
              <div className="bg-zinc-900/50 border border-white/5 rounded-[2rem] p-10 text-2xl text-zinc-200 leading-relaxed font-light backdrop-blur-sm shadow-2xl ring-1 ring-white/5 italic">
                 "{(handwrittenQuestion?.renderData as any).content?.[0]?.content?.[0]?.text}"
              </div>
            </motion.div>

            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1 }}
              className="space-y-6"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-white/5 rounded-xl text-zinc-500">
                    <ScanEye size={18} />
                  </div>
                  <h2 className="text-[10px] font-black text-zinc-500 uppercase tracking-[0.3em]">Student Evidence</h2>
                </div>
                <div className="flex items-center gap-2 px-3 py-1 bg-emerald-500/10 text-emerald-400 rounded-lg text-[10px] font-black uppercase tracking-widest border border-emerald-500/20">
                  <Target size={12} /> High Res Scan
                </div>
              </div>

              <div className="relative aspect-[3/4] bg-zinc-900 rounded-[3rem] overflow-hidden border border-white/5 shadow-[0_50px_100px_rgba(0,0,0,0.6)] group ring-1 ring-white/10">
                <div className="absolute inset-0 flex items-center justify-center">
                  <div className="w-full h-full bg-[url('https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&q=80&w=1000')] bg-cover bg-center group-hover:scale-110 transition-transform duration-[2s]" />
                  <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none" />
                  
                  <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                    <motion.div 
                       animate={{ 
                         opacity: [0, 0.5, 0],
                         top: ["0%", "100%", "0%"]
                       }}
                       transition={{ duration: 4, repeat: Infinity, ease: "linear" }}
                       className="absolute left-0 right-0 h-1 bg-indigo-500/50 blur-sm z-10"
                    />
                  </div>

                  <div className="absolute bottom-10 left-10 right-10 flex items-end justify-between opacity-0 group-hover:opacity-100 transition-all duration-500 translate-y-4 group-hover:translate-y-0">
                    <div className="p-6 bg-black/60 backdrop-blur-xl rounded-2xl border border-white/10 max-w-sm">
                       <p className="text-white font-bold text-lg mb-1 italic leading-tight">"Forces are perfectly balanced in the vertical plane..."</p>
                       <p className="text-zinc-500 text-xs font-mono tracking-tighter">AI DETECTED SEGMENT #12</p>
                    </div>
                    <div className="flex gap-2">
                       <button className="p-4 bg-white rounded-2xl text-black hover:scale-110 transition-transform"><CheckCircle size={20} /></button>
                    </div>
                  </div>
                </div>
              </div>
            </motion.div>
          </div>
        </div>

        {/* Right: Intelligence Side Panel */}
        <aside className="w-[550px] bg-zinc-900 flex flex-col border-l border-white/5 shadow-2xl relative z-10">
          <div className="absolute top-0 right-0 p-8">
             <Zap size={24} className="text-zinc-800" />
          </div>

          <div className="p-10 border-b border-white/5 bg-indigo-600/[0.02]">
            <div className="flex items-center justify-between mb-8">
              <h2 className="text-[10px] font-black text-indigo-400 uppercase tracking-[0.3em] flex items-center gap-2">
                <Sparkles size={16} /> Intelligence Insights
              </h2>
              <span className="text-[10px] px-3 py-1.5 bg-indigo-500/10 text-indigo-400 rounded-xl border border-indigo-500/20 font-black">94.2% SCORE ACCURACY</span>
            </div>
            
            <div className="space-y-8">
              <div className="bg-zinc-950/80 rounded-3xl p-8 border border-white/5 shadow-inner relative overflow-hidden group">
                <div className="absolute top-0 left-0 w-1 h-full bg-indigo-600 opacity-20" />
                <h3 className="text-[10px] font-black text-zinc-600 uppercase tracking-widest mb-4">Neural Transcript</h3>
                <p className="text-zinc-400 text-lg leading-relaxed font-light italic">
                  "The free body diagram shows three forces: Fg acting downwards, Fn perpendicular to the surface... and friction Ff opposing the motion..."
                </p>
                <div className="mt-6 pt-6 border-t border-white/5 flex gap-2 overflow-x-auto no-scrollbar">
                   {['mechanics', 'newton-3', 'vector-calc'].map(tag => (
                     <span key={tag} className="text-[9px] font-bold px-2 py-1 bg-white/5 rounded-md text-zinc-500 border border-white/5 uppercase">#{tag}</span>
                   ))}
                </div>
              </div>

              <div className="flex items-center gap-6 p-6 bg-emerald-500/[0.03] border border-emerald-500/10 rounded-3xl ring-1 ring-emerald-500/5 shadow-xl">
                <div className="p-4 bg-emerald-500/10 rounded-2xl text-emerald-500 shadow-[0_0_20px_rgba(16,185,129,0.2)]">
                  <Award size={28} />
                </div>
                <div>
                  <p className="text-[10px] font-black text-emerald-500 uppercase tracking-widest mb-1">AI Recommendation</p>
                  <p className="text-2xl text-white font-serif tracking-tight">18 <span className="text-zinc-600 text-sm font-sans font-bold">/ {handwrittenQuestion?.points}</span></p>
                  <p className="text-xs text-emerald-500/60 font-medium mt-1">Found 4/4 required vector components.</p>
                </div>
              </div>
            </div>
          </div>

          <div className="p-10 space-y-12 flex-1 overflow-y-auto custom-scrollbar">
            <div className="space-y-6">
              <h3 className="text-[10px] font-black text-zinc-500 uppercase tracking-[0.3em]">Final Assessment Score</h3>
              <div className="flex items-end gap-6">
                <div className="relative group">
                  <div className="absolute -inset-4 bg-indigo-500/20 blur-2xl rounded-full opacity-0 group-hover:opacity-100 transition-opacity duration-500" />
                  <input 
                    type="number" 
                    value={score}
                    onChange={(e) => setScore(parseInt(e.target.value))}
                    className="relative w-40 bg-zinc-950 border border-white/10 rounded-[2.5rem] p-8 text-6xl font-black text-center text-indigo-500 outline-none focus:border-indigo-500/50 transition-all shadow-2xl ring-1 ring-white/5"
                  />
                </div>
                <div className="mb-10 text-zinc-700 font-serif text-3xl">/ {handwrittenQuestion?.points}</div>
              </div>
            </div>

            <div className="space-y-6">
              <div className="flex items-center justify-between">
                <h3 className="text-[10px] font-black text-zinc-500 uppercase tracking-[0.3em] flex items-center gap-2">
                  <MessageSquare size={14} /> Teacher Feedback
                </h3>
                <span className="text-[9px] text-zinc-600 font-mono">MARKDOWN SUPPORTED</span>
              </div>
              <textarea 
                value={feedback}
                onChange={(e) => setFeedback(e.target.value)}
                placeholder="Compose personalized guidance..."
                className="w-full h-64 bg-zinc-950 border border-white/5 rounded-[2.5rem] p-10 text-zinc-300 text-lg font-light leading-relaxed outline-none focus:border-indigo-500/30 transition-all resize-none shadow-2xl ring-1 ring-white/5 placeholder:text-zinc-800"
              />
            </div>
          </div>
        </aside>
      </main>
    </div>
  );
};

export default ManualGrading;
