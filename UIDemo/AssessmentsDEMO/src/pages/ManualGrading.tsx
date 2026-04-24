import React, { useState } from 'react';
import { 
  ChevronLeft, 
  CheckCircle, 
  XCircle, 
  MessageSquare,
  Sparkles,
  Award
} from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAssessmentStore } from '../store/useAssessmentStore';

const ManualGrading = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { submissions, updateSubmission, assessments } = useAssessmentStore();
  
  // For demo purposes, we'll find the submission or use a mock one
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

  const [score, setScore] = useState<number>(0);
  const [feedback, setFeedback] = useState<string>('');

  const handleFinalize = () => {
    updateSubmission(submission.id, { 
      status: 'Graded', 
      totalScore: score + 10, // Assuming 10 points from automated MCQ
      gradedAt: new Date().toISOString()
    });
    alert('Grading Finalized!');
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col w-full font-sans">
      {/* Grading Header */}
      <header className="bg-slate-900 border-b border-slate-800 p-4 sticky top-0 z-50">
        <div className="max-w-[1600px] mx-auto flex items-center justify-between">
          <div className="flex items-center gap-6">
            <button onClick={() => navigate('/')} className="p-2 hover:bg-slate-800 rounded-lg transition-colors text-slate-400 hover:text-white">
              <ChevronLeft />
            </button>
            <div>
              <h1 className="font-bold text-lg">Grading: {assessment?.title}</h1>
              <div className="flex items-center gap-3 mt-1">
                <span className="text-xs px-2 py-0.5 bg-blue-500/20 text-blue-400 rounded-full font-medium border border-blue-500/20">
                  Student: {submission.studentId}
                </span>
                <span className="text-xs text-slate-500">Submitted: {new Date(submission.submittedAt).toLocaleString()}</span>
              </div>
            </div>
          </div>
          <button 
            onClick={handleFinalize}
            className="bg-blue-600 hover:bg-blue-500 px-8 py-2 rounded-xl font-bold transition-all shadow-lg shadow-blue-600/20 flex items-center gap-2"
          >
            <CheckCircle size={18} /> Finalize Grade
          </button>
        </div>
      </header>

      <main className="flex-1 flex w-full">
        {/* Left: Student Upload */}
        <div className="flex-1 bg-black overflow-y-auto p-12 border-r border-slate-800">
          <div className="max-w-3xl mx-auto space-y-12">
            <div className="space-y-4">
              <h2 className="text-sm font-bold text-slate-500 uppercase tracking-widest flex items-center gap-2">
                Question Prompt
              </h2>
              <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 text-xl text-slate-200 leading-relaxed shadow-sm">
                 {(handwrittenQuestion?.renderData as any).content?.[0]?.content?.[0]?.text}
              </div>
            </div>

            <div className="space-y-4">
              <h2 className="text-sm font-bold text-slate-500 uppercase tracking-widest flex items-center gap-2">
                Student Upload
              </h2>
              <div className="relative aspect-[3/4] bg-slate-900 rounded-3xl overflow-hidden border-2 border-slate-800 shadow-2xl group">
                <div className="absolute inset-0 flex items-center justify-center">
                  {/* Mock image placeholder */}
                  <div className="w-full h-full bg-[url('https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&q=80&w=1000')] bg-cover bg-center opacity-50 blur-sm group-hover:blur-none transition-all duration-700" />
                  <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-transparent pointer-events-none" />
                  <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 text-center group-hover:opacity-0 transition-opacity">
                    <p className="text-white font-bold text-2xl drop-shadow-lg">Ali_Physics_Answer.jpg</p>
                    <p className="text-slate-400 text-sm mt-2">Hover to view high-resolution scan</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Right: AI & Teacher Controls */}
        <aside className="w-[500px] bg-slate-900 flex flex-col h-[calc(100vh-80px)] overflow-y-auto border-l border-white/5">
          {/* AI Hints Section */}
          <div className="p-8 border-b border-white/5 bg-blue-600/5">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-sm font-bold text-blue-400 uppercase tracking-widest flex items-center gap-2">
                <Sparkles size={16} /> AI Assistant
              </h2>
              <span className="text-xs px-2 py-1 bg-blue-500/20 text-blue-400 rounded-lg border border-blue-500/20">94% Confidence</span>
            </div>
            
            <div className="space-y-6">
              <div className="bg-slate-950/50 rounded-2xl p-6 border border-blue-500/20">
                <h3 className="text-xs font-bold text-slate-500 uppercase mb-3">Transcribed Text</h3>
                <p className="text-slate-300 italic leading-relaxed">
                  "The free body diagram shows three forces: Fg acting downwards, Fn perpendicular to the surface... and friction Ff opposing the motion..."
                </p>
              </div>
              <div className="flex items-center gap-4 p-4 bg-green-500/10 border border-green-500/20 rounded-xl">
                <Award className="text-green-500" />
                <div>
                  <p className="text-sm text-green-100 font-bold">Suggested Score: 18 / 20</p>
                  <p className="text-xs text-green-500/80">Correct identification of all vector components.</p>
                </div>
              </div>
            </div>
          </div>

          {/* Teacher Inputs */}
          <div className="p-8 space-y-8 flex-1">
            <div className="space-y-4">
              <h3 className="text-sm font-bold text-slate-500 uppercase tracking-widest">Assign Score</h3>
              <div className="flex items-end gap-4">
                <input 
                  type="number" 
                  value={score}
                  onChange={(e) => setScore(parseInt(e.target.value))}
                  className="w-32 bg-slate-950 border border-slate-800 rounded-2xl p-6 text-4xl font-bold text-center text-blue-400 outline-none focus:border-blue-500 transition-all ring-1 ring-white/5"
                />
                <span className="text-2xl text-slate-600 font-medium mb-6">/ {handwrittenQuestion?.points}</span>
              </div>
            </div>

            <div className="space-y-4">
              <h3 className="text-sm font-bold text-slate-500 uppercase tracking-widest flex items-center gap-2">
                <MessageSquare size={16} /> Feedback to Student
              </h3>
              <textarea 
                value={feedback}
                onChange={(e) => setFeedback(e.target.value)}
                placeholder="Great work on the diagrams. Try to explain the friction coefficient derivation more clearly next time..."
                className="w-full h-48 bg-slate-950 border border-slate-800 rounded-2xl p-6 text-slate-300 outline-none focus:border-blue-500 transition-all resize-none ring-1 ring-white/5"
              />
            </div>
          </div>
        </aside>
      </main>
    </div>
  );
};

export default ManualGrading;
