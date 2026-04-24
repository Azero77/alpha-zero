import React, { useState } from 'react';
import { 
  ChevronLeft, 
  Clock, 
  Send, 
  ShieldCheck,
  AlertCircle
} from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAssessmentStore } from '../store/useAssessmentStore';

const QuizTaker = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { assessments, addSubmission } = useAssessmentStore();
  const assessment = assessments.find(a => a.id === id) || assessments[0];
  const content = assessment.versions[0].content;

  const [answers, setAnswers] = useState<Record<string, any>>({});

  const handleMcqSelect = (questionId: string, choiceId: string) => {
    setAnswers(prev => ({ ...prev, [questionId]: choiceId }));
  };

  const handleSubmit = () => {
    const submission = {
      id: `sub-${Date.now()}`,
      tenantId: assessment.tenantId,
      assessmentId: assessment.id,
      assessmentVersionId: assessment.versions[0].id,
      studentId: 'student-123',
      status: 'Submitted' as any,
      submittedAt: new Date().toISOString(),
      responses: { answers }
    };
    addSubmission(submission);
    alert('Quiz Submitted! Redirecting to results...');
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col w-full">
      {/* Quiz Header */}
      <header className="bg-slate-900 border-b border-slate-800 p-4 sticky top-0 z-50">
        <div className="max-w-4xl mx-auto flex items-center justify-between">
          <div className="flex items-center gap-4">
            <button onClick={() => navigate('/')} className="p-2 hover:bg-slate-800 rounded-lg transition-colors">
              <ChevronLeft />
            </button>
            <div>
              <h1 className="font-bold text-lg">{assessment.title}</h1>
              <p className="text-xs text-slate-400">Passing Score: {assessment.passingScore}%</p>
            </div>
          </div>
          <div className="flex items-center gap-6">
            <div className="flex items-center gap-2 text-blue-400 font-mono font-bold">
              <Clock size={18} /> 45:00
            </div>
            <button 
              onClick={handleSubmit}
              className="bg-green-600 hover:bg-green-500 px-6 py-2 rounded-lg font-bold transition-all flex items-center gap-2"
            >
              <Send size={18} /> Submit
            </button>
          </div>
        </div>
      </header>

      <main className="flex-1 max-w-4xl mx-auto w-full py-12 px-6">
        <div className="bg-blue-500/10 border border-blue-500/20 rounded-xl p-4 mb-10 flex gap-4 items-start">
          <ShieldCheck className="text-blue-500 shrink-0" />
          <p className="text-sm text-blue-200">
            <strong>Exam Integrity:</strong> Your session is being monitored. Switching tabs or leaving this page may result in immediate submission.
          </p>
        </div>

        <div className="space-y-12">
          {content.items.map((item) => {
            if (item.type === 'Paragraph') {
              return (
                <div key={item.id} className="prose prose-invert max-w-none text-slate-300">
                  {/* Simplified text rendering for demo */}
                  <p>{(item.renderData as any).content?.[0]?.content?.[0]?.text}</p>
                </div>
              );
            }

            if (item.type === 'Question' && item.questionType === 'MCQ') {
              return (
                <div key={item.id} className="bg-slate-900 border border-slate-800 rounded-2xl p-8 shadow-xl">
                  <div className="flex justify-between items-start mb-6">
                    <h3 className="text-xl font-medium text-white">
                      {(item.renderData as any).content?.[0]?.content?.[0]?.text}
                    </h3>
                    <span className="text-xs font-bold text-slate-500 uppercase">{item.points} Points</span>
                  </div>

                  <div className="grid gap-3">
                    {item.gradingData?.choices?.map((choice) => (
                      <button
                        key={choice.id}
                        onClick={() => handleMcqSelect(item.id, choice.id)}
                        className={`text-left p-4 rounded-xl border transition-all flex items-center justify-between group ${
                          answers[item.id] === choice.id 
                          ? 'bg-blue-600/20 border-blue-500 text-blue-100 ring-1 ring-blue-500' 
                          : 'bg-slate-800/50 border-slate-700 text-slate-400 hover:border-slate-500'
                        }`}
                      >
                        <span>{(choice.renderData as any).text}</span>
                        <div className={`w-5 h-5 rounded-full border-2 flex items-center justify-center ${
                          answers[item.id] === choice.id ? 'border-blue-400' : 'border-slate-600'
                        }`}>
                          {answers[item.id] === choice.id && <div className="w-2.5 h-2.5 bg-blue-400 rounded-full" />}
                        </div>
                      </button>
                    ))}
                  </div>
                </div>
              );
            }

            if (item.type === 'Question' && item.questionType === 'Handwritten') {
              return (
                <div key={item.id} className="bg-slate-900 border border-slate-800 rounded-2xl p-8 shadow-xl">
                  <div className="flex justify-between items-start mb-6">
                    <h3 className="text-xl font-medium text-white">
                      {(item.renderData as any).content?.[0]?.content?.[0]?.text}
                    </h3>
                    <span className="text-xs font-bold text-slate-500 uppercase">{item.points} Points</span>
                  </div>
                  
                  <div className="border-2 border-dashed border-slate-700 rounded-2xl p-12 flex flex-col items-center justify-center bg-slate-950/50 group hover:border-blue-500/50 transition-colors">
                    <AlertCircle className="text-slate-500 mb-4 group-hover:text-blue-400 transition-colors" size={48} />
                    <p className="text-slate-400 font-medium mb-2 text-center text-lg">Upload your handwritten work</p>
                    <p className="text-slate-500 text-sm text-center mb-6 max-w-xs">Please take a clear photo of your working out. Supported formats: JPG, PNG, PDF.</p>
                    <button className="bg-slate-800 hover:bg-slate-700 px-8 py-3 rounded-xl font-bold border border-slate-700 transition-all shadow-lg text-lg">
                      Choose Files
                    </button>
                  </div>
                </div>
              );
            }

            return null;
          })}
        </div>
      </main>
    </div>
  );
};

export default QuizTaker;
