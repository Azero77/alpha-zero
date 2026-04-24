import { useState, useEffect } from 'react';
import { 
  ChevronLeft, 
  Clock, 
  ShieldCheck,
  AlertCircle,
  FileText,
  Info
} from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAssessmentStore } from '../store/useAssessmentStore';
import { motion, AnimatePresence } from 'framer-motion';

const QuizTaker = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { assessments, addSubmission } = useAssessmentStore();
  const assessment = assessments.find(a => a.id === id) || assessments[0];
  const content = assessment.versions[0].content;

  const [answers, setAnswers] = useState<Record<string, any>>({});
  const [timeLeft, setTimeLeft] = useState(2700); // 45 minutes in seconds

  useEffect(() => {
    const timer = setInterval(() => {
      setTimeLeft(prev => prev > 0 ? prev - 1 : 0);
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  const formatTime = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  const handleMcqSelect = (questionId: string, choiceId: string) => {
    setAnswers(prev => ({ ...prev, [questionId]: choiceId }));
  };

  const handleSubmit = () => {
    const submission = {
      id: `sub-${Date.now()}`,
      tenantId: assessment.tenantId,
      assessmentId: assessment.id,
      assessmentVersionId: assessment.versions[0].id,
      studentId: 'Ali Hassan',
      status: 'Submitted' as any,
      submittedAt: new Date().toISOString(),
      responses: { answers }
    };
    addSubmission(submission);
    alert('Submission finalized and encrypted. Redirecting to dashboard...');
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 flex flex-col w-full selection:bg-indigo-500/30 font-sans">
      {/* Immersive Header */}
      <header className="bg-zinc-900/80 backdrop-blur-2xl border-b border-white/5 p-6 sticky top-0 z-50 shadow-2xl">
        <div className="max-w-5xl mx-auto flex items-center justify-between">
          <div className="flex items-center gap-6">
            <button onClick={() => navigate('/')} className="p-2.5 hover:bg-white/5 rounded-2xl transition-all text-zinc-500 hover:text-white border border-transparent hover:border-white/10">
              <ChevronLeft size={22} />
            </button>
            <div className="h-10 w-[1px] bg-white/10 mx-2" />
            <div>
              <h1 className="font-serif text-2xl tracking-tight">{assessment.title}</h1>
              <div className="flex items-center gap-4 mt-1">
                <span className="text-[10px] font-black uppercase tracking-[0.2em] text-zinc-500 flex items-center gap-1.5">
                  <FileText size={12} className="text-indigo-400" /> Section 1 of 4
                </span>
                <span className="text-[10px] font-black uppercase tracking-[0.2em] text-zinc-500 flex items-center gap-1.5">
                  <Info size={12} className="text-emerald-400" /> {assessment.passingScore}% to pass
                </span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-10">
            <div className="flex flex-col items-end">
              <div className={`flex items-center gap-2 font-mono text-2xl font-black transition-colors ${timeLeft < 300 ? 'text-red-500' : 'text-indigo-400'}`}>
                <Clock size={20} /> {formatTime(timeLeft)}
              </div>
              <p className="text-[9px] font-black uppercase tracking-widest text-zinc-600 mt-1">Remaining Time</p>
            </div>
            <button 
              onClick={handleSubmit}
              className="bg-emerald-600 hover:bg-emerald-500 text-white px-8 py-3 rounded-2xl font-black uppercase tracking-widest text-xs transition-all shadow-[0_0_30px_rgba(16,185,129,0.2)] hover:scale-105 active:scale-95"
            >
              Submit Exam
            </button>
          </div>
        </div>
      </header>

      <main className="flex-1 max-w-4xl mx-auto w-full py-16 px-6">
        {/* Integrity Banner */}
        <motion.div 
          initial={{ opacity: 0, y: -10 }}
          animate={{ opacity: 1, y: 0 }}
          className="bg-zinc-900 border border-white/5 rounded-3xl p-6 mb-16 flex gap-5 items-center ring-1 ring-white/5 shadow-2xl relative overflow-hidden"
        >
          <div className="absolute top-0 left-0 bottom-0 w-1.5 bg-indigo-500 shadow-[0_0_15px_rgba(79,70,229,0.5)]" />
          <div className="p-3 bg-indigo-500/10 rounded-2xl text-indigo-400">
            <ShieldCheck size={28} />
          </div>
          <div>
            <p className="text-sm font-bold text-zinc-200 mb-1">Advanced Integrity Guard Active</p>
            <p className="text-xs text-zinc-500 font-medium">Your biometric profile and environmental focus are being analyzed to ensure fair evaluation.</p>
          </div>
        </motion.div>

        <div className="space-y-20">
          {content.items.map((item, index) => {
            if (item.type === 'Paragraph') {
              return (
                <motion.div 
                  initial={{ opacity: 0 }}
                  whileInView={{ opacity: 1 }}
                  key={item.id} 
                  className="prose prose-invert prose-zinc max-w-none"
                >
                  <p className="text-xl text-zinc-400 leading-relaxed font-light italic">
                    {(item.renderData as any).content?.[0]?.content?.[0]?.text}
                  </p>
                </motion.div>
              );
            }

            if (item.type === 'Question' && item.questionType === 'MCQ') {
              return (
                <motion.div 
                  initial={{ opacity: 0, x: -20 }}
                  whileInView={{ opacity: 1, x: 0 }}
                  viewport={{ once: true, margin: "-100px" }}
                  key={item.id} 
                  className="bg-zinc-900/40 border border-white/5 rounded-[2.5rem] p-12 shadow-2xl relative group"
                >
                  <div className="absolute -left-6 top-1/2 -translate-y-1/2 flex flex-col gap-1">
                    <span className="text-[10px] font-black text-zinc-700 uppercase vertical-text">Question {index}</span>
                  </div>

                  <div className="flex justify-between items-start mb-10">
                    <h3 className="text-3xl font-serif text-white max-w-xl leading-tight">
                      {(item.renderData as any).content?.[0]?.content?.[0]?.text}
                    </h3>
                    <div className="px-4 py-2 bg-white/5 rounded-xl border border-white/5 text-[10px] font-black text-zinc-500 uppercase tracking-widest">
                      {item.points} PTS
                    </div>
                  </div>

                  <div className="grid gap-4">
                    {item.gradingData?.choices?.map((choice) => (
                      <button
                        key={choice.id}
                        onClick={() => handleMcqSelect(item.id, choice.id)}
                        className={`text-left p-6 rounded-2xl border-2 transition-all flex items-center justify-between group/choice relative overflow-hidden ${
                          answers[item.id] === choice.id 
                          ? 'bg-indigo-600/10 border-indigo-500 text-indigo-100 shadow-[0_0_20px_rgba(79,70,229,0.15)]' 
                          : 'bg-zinc-950/30 border-white/5 text-zinc-400 hover:border-zinc-700 hover:bg-zinc-900/50'
                        }`}
                      >
                        <span className={`text-lg transition-colors ${answers[item.id] === choice.id ? 'font-bold' : 'font-light'}`}>
                          {(choice.renderData as any).text}
                        </span>
                        
                        <div className={`w-6 h-6 rounded-full border-2 transition-all flex items-center justify-center ${
                          answers[item.id] === choice.id ? 'border-indigo-400 bg-indigo-500 shadow-[0_0_10px_rgba(79,70,229,0.5)]' : 'border-zinc-800'
                        }`}>
                          <AnimatePresence>
                            {answers[item.id] === choice.id && (
                              <motion.div 
                                initial={{ scale: 0 }}
                                animate={{ scale: 1 }}
                                exit={{ scale: 0 }}
                                className="w-2.5 h-2.5 bg-white rounded-full" 
                              />
                            )}
                          </AnimatePresence>
                        </div>
                      </button>
                    ))}
                  </div>
                </motion.div>
              );
            }

            if (item.type === 'Question' && item.questionType === 'Handwritten') {
              return (
                <motion.div 
                  initial={{ opacity: 0, x: -20 }}
                  whileInView={{ opacity: 1, x: 0 }}
                  viewport={{ once: true, margin: "-100px" }}
                  key={item.id} 
                  className="bg-zinc-900/40 border border-white/5 rounded-[2.5rem] p-12 shadow-2xl relative"
                >
                  <div className="flex justify-between items-start mb-10">
                    <h3 className="text-3xl font-serif text-white max-w-xl leading-tight">
                      {(item.renderData as any).content?.[0]?.content?.[0]?.text}
                    </h3>
                    <div className="px-4 py-2 bg-white/5 rounded-xl border border-white/5 text-[10px] font-black text-zinc-500 uppercase tracking-widest">
                      {item.points} PTS
                    </div>
                  </div>
                  
                  <div className="border-2 border-dashed border-zinc-800 rounded-[2rem] p-20 flex flex-col items-center justify-center bg-zinc-950/50 group hover:border-indigo-500/30 transition-all duration-500 hover:shadow-[0_0_50px_rgba(79,70,229,0.05)]">
                    <div className="w-20 h-20 rounded-3xl bg-indigo-500/5 border border-indigo-500/10 flex items-center justify-center text-indigo-400 mb-8 group-hover:scale-110 transition-transform duration-500 group-hover:text-indigo-300 group-hover:shadow-[0_0_20px_rgba(79,70,229,0.2)]">
                      <AlertCircle size={32} />
                    </div>
                    <p className="text-zinc-200 font-bold mb-3 text-2xl text-center">Capture Written Solution</p>
                    <p className="text-zinc-500 text-sm text-center mb-10 max-w-sm font-medium leading-relaxed">Please ensure the camera is perpendicular to the page. High-resolution scans preferred for AI transcription accuracy.</p>
                    <button className="bg-zinc-100 hover:bg-white text-zinc-950 px-10 py-4 rounded-2xl font-black uppercase tracking-widest text-xs transition-all shadow-xl active:scale-95">
                      Upload from Device
                    </button>
                  </div>
                </motion.div>
              );
            }

            return null;
          })}
        </div>
      </main>

      {/* Progress Footer */}
      <footer className="bg-zinc-900/80 backdrop-blur-2xl border-t border-white/5 p-4 sticky bottom-0 z-50">
        <div className="max-w-4xl mx-auto flex items-center gap-6">
          <div className="text-[10px] font-black uppercase tracking-[0.2em] text-zinc-600 whitespace-nowrap">Exam Progress</div>
          <div className="flex-1 h-1.5 bg-white/5 rounded-full overflow-hidden">
            <motion.div 
              initial={{ width: 0 }}
              animate={{ width: `${(Object.keys(answers).length / content.items.filter(i => i.type === 'Question').length) * 100}%` }}
              className="h-full bg-gradient-to-r from-indigo-500 to-purple-500 shadow-[0_0_15px_rgba(79,70,229,0.5)]"
            />
          </div>
          <div className="text-[10px] font-black uppercase tracking-[0.2em] text-indigo-400 whitespace-nowrap">
            {Object.keys(answers).length} of {content.items.filter(i => i.type === 'Question').length} Answered
          </div>
        </div>
      </footer>
    </div>
  );
};

export default QuizTaker;
