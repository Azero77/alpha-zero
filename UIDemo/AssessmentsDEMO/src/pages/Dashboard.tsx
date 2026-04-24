import { useNavigate } from 'react-router-dom';
import { useAssessmentStore } from '../store/useAssessmentStore';
import { 
  Plus, 
  BookOpen, 
  GraduationCap, 
  CheckCircle2, 
  Clock, 
  ArrowUpRight,
  Sparkles,
  Zap,
  Users
} from 'lucide-react';
import { motion } from 'framer-motion';

const container = {
  hidden: { opacity: 0 },
  show: {
    opacity: 1,
    transition: {
      staggerChildren: 0.1
    }
  }
} as const;

const item = {
  hidden: { opacity: 0, y: 20 },
  show: { opacity: 1, y: 0, transition: { type: 'spring', stiffness: 300, damping: 24 } }
} as const;

const Dashboard = () => {
  const navigate = useNavigate();
  const { assessments } = useAssessmentStore();

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 w-full overflow-x-hidden">
      {/* Background Decorative Element */}
      <div className="absolute top-0 left-1/2 -translate-x-1/2 w-full h-[500px] bg-indigo-500/10 blur-[120px] rounded-full pointer-events-none" />

      <main className="max-w-[1200px] mx-auto px-6 py-16 relative">
        {/* Hero Section */}
        <header className="flex flex-col md:flex-row justify-between items-start md:items-end gap-8 mb-24">
          <motion.div 
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            className="max-w-2xl"
          >
            <div className="inline-flex items-center gap-2 px-3 py-1 bg-white/5 border border-white/10 rounded-full text-xs font-medium text-indigo-400 mb-6">
              <Sparkles size={14} />
              <span>SaaS Assessments Redefined</span>
            </div>
            <h1 className="text-6xl md:text-8xl font-serif mb-6 leading-[0.9]">
              Elite Academic <br />
              <span className="text-zinc-500 italic">Evaluations</span>
            </h1>
            <p className="text-xl text-zinc-400 font-light max-w-lg leading-relaxed">
              Design professional exams with our block-based engine. 
              Hybrid automated grading & AI-assisted manual review.
            </p>
          </motion.div>
          
          <motion.button 
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            onClick={() => navigate('/builder')}
            className="group bg-indigo-600 hover:bg-indigo-500 text-white px-8 py-4 rounded-2xl font-bold transition-all shadow-[0_0_40px_rgba(79,70,229,0.3)] flex items-center gap-3 relative overflow-hidden"
          >
            <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/10 to-transparent -translate-x-full group-hover:translate-x-full transition-transform duration-1000" />
            <Plus size={22} /> 
            <span>Build New Exam</span>
          </motion.button>
        </header>

        <div className="grid lg:grid-cols-12 gap-12">
          {/* Left Column: Teacher View */}
          <motion.section 
            variants={container}
            initial="hidden"
            animate="show"
            className="lg:col-span-5 space-y-8"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-indigo-500/10 rounded-xl flex items-center justify-center text-indigo-400 border border-indigo-500/20">
                  <BookOpen size={20} />
                </div>
                <h2 className="text-2xl font-serif">Grading Queue</h2>
              </div>
              <span className="text-xs font-medium px-2 py-1 bg-white/5 rounded-md text-zinc-500">1 Pending</span>
            </div>

            <motion.div variants={item} className="group relative">
              <div className="absolute -inset-0.5 bg-gradient-to-r from-indigo-500 to-purple-500 rounded-3xl blur opacity-0 group-hover:opacity-15 transition duration-500" />
              <button 
                onClick={() => navigate('/grade/sub-1')}
                className="relative w-full bg-zinc-900/50 border border-white/5 backdrop-blur-sm p-6 rounded-3xl flex items-center justify-between hover:bg-zinc-900 transition-all text-left"
              >
                <div className="flex items-center gap-5">
                  <div className="relative">
                    <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-zinc-800 to-zinc-950 flex items-center justify-center text-indigo-400 font-bold border border-white/10">
                      AH
                    </div>
                    <div className="absolute -bottom-1 -right-1 w-4 h-4 bg-green-500 rounded-full border-4 border-zinc-950" />
                  </div>
                  <div>
                    <h3 className="font-bold text-lg text-zinc-100">Ali Hassan</h3>
                    <p className="text-sm text-zinc-500 flex items-center gap-2">
                      <Clock size={14} /> 2 hours ago • Physics 101
                    </p>
                  </div>
                </div>
                <div className="flex flex-col items-end gap-2">
                  <span className="inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider px-2 py-1 bg-amber-500/10 text-amber-500 rounded-lg border border-amber-500/20">
                    <Zap size={10} /> Hybrid Review
                  </span>
                  <div className="w-8 h-8 rounded-full bg-white/5 flex items-center justify-center text-zinc-500 group-hover:bg-indigo-500 group-hover:text-white transition-all">
                    <ArrowUpRight size={18} />
                  </div>
                </div>
              </button>
            </motion.div>

            {/* Quick Stats */}
            <div className="grid grid-cols-2 gap-4">
              <motion.div variants={item} className="bg-zinc-900/30 border border-white/5 p-6 rounded-3xl">
                <p className="text-zinc-500 text-sm mb-1 flex items-center gap-2 font-medium">
                  <Users size={14} /> Enrolled
                </p>
                <p className="text-3xl font-serif">1.2k</p>
              </motion.div>
              <motion.div variants={item} className="bg-zinc-900/30 border border-white/5 p-6 rounded-3xl">
                <p className="text-zinc-500 text-sm mb-1 flex items-center gap-2 font-medium">
                  <CheckCircle2 size={14} /> Pass Rate
                </p>
                <p className="text-3xl font-serif">84%</p>
              </motion.div>
            </div>
          </motion.section>

          {/* Right Column: Student View */}
          <motion.section 
            variants={container}
            initial="hidden"
            animate="show"
            className="lg:col-span-7 space-y-8"
          >
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-emerald-500/10 rounded-xl flex items-center justify-center text-emerald-400 border border-emerald-500/20">
                <GraduationCap size={20} />
              </div>
              <h2 className="text-2xl font-serif">Active Assessments</h2>
            </div>

            <div className="grid sm:grid-cols-2 gap-6">
              {assessments.map(a => (
                <motion.div 
                  key={a.id} 
                  variants={item}
                  className="group relative"
                >
                  <div className="absolute -inset-0.5 bg-gradient-to-r from-emerald-500 to-teal-500 rounded-3xl blur opacity-0 group-hover:opacity-10 transition duration-500" />
                  <div className="relative bg-zinc-900/50 border border-white/5 p-8 rounded-3xl backdrop-blur-sm flex flex-col h-full hover:bg-zinc-900 transition-all">
                    <div className="flex justify-between items-start mb-6">
                      <div className="p-3 bg-white/5 rounded-2xl group-hover:text-emerald-400 transition-colors">
                        <BookOpen size={24} />
                      </div>
                      <span className="text-[10px] font-bold uppercase tracking-[0.2em] px-2 py-1 bg-white/5 text-zinc-500 rounded-md border border-white/10">
                        {a.type}
                      </span>
                    </div>
                    
                    <h3 className="text-2xl font-serif mb-2 group-hover:translate-x-1 transition-transform">{a.title}</h3>
                    <p className="text-zinc-500 text-sm leading-relaxed mb-8 flex-1 font-light">
                      {a.description}
                    </p>
                    
                    <div className="flex items-center justify-between pt-6 border-t border-white/5">
                      <div className="flex gap-4 text-[11px] text-zinc-500 font-bold uppercase tracking-wider">
                        <span className="flex items-center gap-1.5"><Clock size={14} /> 45m</span>
                        <span className="flex items-center gap-1.5"><CheckCircle2 size={14} /> {a.passingScore}%</span>
                      </div>
                      <button 
                        onClick={() => navigate(`/quiz/${a.id}`)}
                        className="bg-zinc-100 hover:bg-white text-zinc-950 px-6 py-2.5 rounded-xl text-xs font-black uppercase tracking-widest transition-all"
                      >
                        Start
                      </button>
                    </div>
                  </div>
                </motion.div>
              ))}
            </div>
          </motion.section>
        </div>
      </main>
    </div>
  );
};

export default Dashboard;
