import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAssessmentStore } from '../store/useAssessmentStore';
import { 
  Plus, 
  BookOpen, 
  GraduationCap, 
  CheckCircle2, 
  Clock, 
  ArrowRight 
} from 'lucide-react';

const Dashboard = () => {
  const navigate = useNavigate();
  const { assessments, submissions } = useAssessmentStore();

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 p-8 w-full max-w-6xl mx-auto">
      <header className="flex justify-between items-center mb-12">
        <div>
          <h1 className="text-3xl font-bold text-white mb-2">AlphaZero Assessments</h1>
          <p className="text-slate-400">Production UI Demo for Teacher & Student Workflows</p>
        </div>
        <button 
          onClick={() => navigate('/builder')}
          className="bg-blue-600 hover:bg-blue-500 px-6 py-3 rounded-xl font-bold transition-all shadow-lg shadow-blue-600/20 flex items-center gap-2"
        >
          <Plus size={20} /> Create Assessment
        </button>
      </header>

      <div className="grid md:grid-cols-2 gap-8">
        {/* Teacher Section */}
        <section className="space-y-6">
          <div className="flex items-center gap-3 mb-2">
            <div className="p-2 bg-purple-500/20 rounded-lg text-purple-400">
              <BookOpen size={20} />
            </div>
            <h2 className="text-xl font-bold">Teacher Dashboard</h2>
          </div>

          <div className="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
            <div className="p-4 border-b border-slate-800 bg-slate-800/30 text-xs font-bold text-slate-500 uppercase tracking-widest">
              Submissions for Grading
            </div>
            <div className="divide-y divide-slate-800">
              <button 
                onClick={() => navigate('/grade/sub-1')}
                className="w-full p-4 flex items-center justify-between hover:bg-white/5 transition-colors group"
              >
                <div className="flex items-center gap-4 text-left">
                  <div className="w-10 h-10 rounded-full bg-slate-800 flex items-center justify-center text-slate-400">AH</div>
                  <div>
                    <p className="font-medium text-slate-200">Ali Hassan</p>
                    <p className="text-xs text-slate-500">Physics Midterm • 2h ago</p>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-xs px-2 py-1 bg-yellow-500/10 text-yellow-500 rounded border border-yellow-500/20 font-medium">Needs Review</span>
                  <ArrowRight size={16} className="text-slate-600 group-hover:translate-x-1 transition-transform" />
                </div>
              </button>
            </div>
          </div>
        </section>

        {/* Student Section */}
        <section className="space-y-6">
          <div className="flex items-center gap-3 mb-2">
            <div className="p-2 bg-green-500/20 rounded-lg text-green-400">
              <GraduationCap size={20} />
            </div>
            <h2 className="text-xl font-bold">Student View</h2>
          </div>

          <div className="grid gap-4">
            {assessments.map(a => (
              <div key={a.id} className="bg-slate-900 border border-slate-800 rounded-2xl p-6 hover:border-blue-500/50 transition-all shadow-xl group">
                <div className="flex justify-between items-start mb-4">
                  <div>
                    <h3 className="font-bold text-lg text-white group-hover:text-blue-400 transition-colors">{a.title}</h3>
                    <p className="text-sm text-slate-500">{a.description}</p>
                  </div>
                  <span className="text-xs px-2 py-1 bg-blue-500/10 text-blue-500 rounded border border-blue-500/20 font-bold uppercase tracking-wider">
                    {a.type}
                  </span>
                </div>
                
                <div className="flex items-center justify-between mt-6">
                  <div className="flex gap-4 text-xs text-slate-400 font-medium">
                    <span className="flex items-center gap-1.5"><Clock size={14} /> 45 Mins</span>
                    <span className="flex items-center gap-1.5"><CheckCircle2 size={14} /> {a.passingScore}% Pass</span>
                  </div>
                  <button 
                    onClick={() => navigate(`/quiz/${a.id}`)}
                    className="bg-slate-800 hover:bg-slate-700 text-white px-5 py-2 rounded-lg text-sm font-bold border border-slate-700 transition-all"
                  >
                    Start Exam
                  </button>
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>
    </div>
  );
};

export default Dashboard;
