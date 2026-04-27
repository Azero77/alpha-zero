import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AppLayout } from './layouts/AppLayout';
import { VideoDashboard } from './features/videos/VideoDashboard';
import { QuizDashboard } from './features/quizzes/QuizDashboard';
import { CourseArchitect } from './features/courses/CourseArchitect';
import { SubjectManager } from './features/courses/SubjectManager';
import { LearnerDashboard } from './features/learner/LearnerDashboard';
import { CourseViewer } from './features/learner/CourseViewer';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
    },
  },
});

const DashboardHome = () => (
  <div className="space-y-8 animate-in fade-in duration-700">
    <div className="bg-slate-900 rounded-3xl p-12 text-white relative overflow-hidden shadow-2xl">
      <div className="relative z-10 space-y-4">
        <h1 className="text-5xl font-black tracking-tighter leading-none">System <span className="text-primary-500">Operational</span></h1>
        <p className="text-slate-400 max-w-md font-medium leading-relaxed text-lg">Your global learning infrastructure is synchronized with the edge network.</p>
      </div>
      <div className="absolute right-0 top-0 bottom-0 w-1/3 bg-gradient-to-l from-primary-600/20 to-transparent" />
    </div>

    <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
      <div className="bg-white dark:bg-slate-950 p-10 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-sm space-y-6">
        <h3 className="font-bold text-slate-400 uppercase tracking-[0.2em] text-[10px]">Active Principals</h3>
        <p className="text-5xl font-black tracking-tighter">1,284</p>
        <div className="h-1 bg-slate-100 dark:bg-slate-900 rounded-full overflow-hidden">
          <div className="h-full bg-slate-900 dark:bg-white w-3/4" />
        </div>
      </div>
      <div className="bg-white dark:bg-slate-950 p-10 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-sm space-y-6">
        <h3 className="font-bold text-slate-400 uppercase tracking-[0.2em] text-[10px]">Queue Depth</h3>
        <p className="text-5xl font-black tracking-tighter text-amber-500">3</p>
        <p className="text-xs text-slate-500 font-bold uppercase tracking-widest">Transcoding Assets</p>
      </div>
      <div className="bg-white dark:bg-slate-950 p-10 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-sm space-y-6">
        <h3 className="font-bold text-slate-400 uppercase tracking-[0.2em] text-[10px]">Registry Volume</h3>
        <p className="text-5xl font-black tracking-tighter">42.8 <span className="text-lg text-slate-400">GB</span></p>
        <p className="text-xs text-slate-500 font-bold uppercase tracking-widest">Of 500GB Committed</p>
      </div>
    </div>
  </div>
);

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<AppLayout />}>
            <Route index element={<DashboardHome />} />
            <Route path="videos" element={<VideoDashboard />} />
            <Route path="quizzes" element={<QuizDashboard />} />
            <Route path="courses" element={<CourseArchitect />} />
            <Route path="subjects" element={<SubjectManager />} />
            <Route path="learn" element={<LearnerDashboard />} />
            <Route path="learn/:id" element={<CourseViewer />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
