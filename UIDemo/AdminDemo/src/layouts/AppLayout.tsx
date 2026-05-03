import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { LayoutDashboard, Video, FileText, BookOpen, Layers, Settings, LogOut, ChevronRight, User, ShieldCheck } from 'lucide-react';
import { clsx } from 'clsx';

const SidebarItem = ({ to, icon: Icon, label }: { to: string, icon: any, label: string }) => (
  <NavLink
    to={to}
    className={({ isActive }) => clsx(
      "flex items-center justify-between px-3 py-2 rounded-md text-sm transition-all group",
      isActive 
        ? "bg-slate-900 text-white dark:bg-white dark:text-slate-950 font-semibold shadow-lg shadow-slate-900/10" 
        : "text-slate-500 hover:text-slate-900 dark:hover:text-slate-100 hover:bg-slate-50 dark:hover:bg-slate-900/50"
    )}
  >
    {({ isActive }) => (
      <>
        <div className="flex items-center gap-2.5">
          <Icon size={16} strokeWidth={isActive ? 2.5 : 2} className={clsx("transition-transform group-active:scale-95")} />
          <span>{label}</span>
        </div>
        <ChevronRight size={14} className={clsx("opacity-0 transition-all -translate-x-2 group-hover:opacity-100 group-hover:translate-x-0", isActive && "opacity-0")} />
      </>
    )}
  </NavLink>
);

export const AppLayout = () => {
  const location = useLocation();
  const isLearnerMode = location.pathname.startsWith('/learn');

  return (
    <div className="flex h-screen bg-white dark:bg-slate-950 overflow-hidden w-full font-inter">
      {/* Sidebar */}
      <aside className="w-64 border-r border-slate-200 dark:border-slate-800 flex flex-col h-full bg-slate-50/30 dark:bg-slate-900/10">
        <div className="p-6 flex items-center gap-2.5">
          <div className="w-8 h-8 bg-slate-900 dark:bg-white rounded flex items-center justify-center">
            <Layers className="text-white dark:text-slate-950" size={18} strokeWidth={2.5} />
          </div>
          <h1 className="text-lg font-bold tracking-tight uppercase italic text-slate-900 dark:text-white">AlphaZero</h1>
        </div>

        {/* Mode Switcher */}
        <div className="px-6 mb-8">
           <div className="p-1 bg-slate-100 dark:bg-slate-900 rounded-lg flex gap-1 border border-slate-200 dark:border-slate-800">
             <NavLink to="/" className={({ isActive }) => clsx("flex-1 flex items-center justify-center gap-2 py-1.5 rounded-md text-[10px] font-black uppercase tracking-widest transition-all", isActive && !isLearnerMode ? "bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm border border-slate-200 dark:border-slate-700" : "text-slate-400 hover:text-slate-600")}>
               <ShieldCheck size={12} />
               Architect
             </NavLink>
             <NavLink to="/learn" className={({ isActive }) => clsx("flex-1 flex items-center justify-center gap-2 py-1.5 rounded-md text-[10px] font-black uppercase tracking-widest transition-all", isActive || isLearnerMode ? "bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm border border-slate-200 dark:border-slate-700" : "text-slate-400 hover:text-slate-600")}>
               <User size={12} />
               Learner
             </NavLink>
           </div>
        </div>

        <div className="px-3 space-y-1 flex-1 overflow-y-auto custom-scrollbar">
          <div className="px-3 py-2 text-[10px] font-bold uppercase tracking-[0.15em] text-slate-400 mb-1">
            {isLearnerMode ? "Learning Dashboard" : "Registry Control"}
          </div>
          
          {!isLearnerMode ? (
            <>
              <SidebarItem to="/" icon={LayoutDashboard} label="System Overview" />
              <SidebarItem to="/courses" icon={BookOpen} label="Course Architect" />
              <SidebarItem to="/subjects" icon={Layers} label="Taxonomy Manager" />
              <SidebarItem to="/videos" icon={Video} label="Video Pipeline" />
              <SidebarItem to="/quizzes" icon={FileText} label="Quiz Builder" />
            </>
          ) : (
            <>
              <SidebarItem to="/learn" icon={LayoutDashboard} label="My Path" />
              <SidebarItem to="/courses" icon={BookOpen} label="Explore Catalog" />
            </>
          )}
        </div>

        <div className="p-4 border-t border-slate-200 dark:border-slate-800 space-y-1">
          <SidebarItem to="/settings" icon={Settings} label="Preferences" />
          <button className="flex items-center gap-2.5 px-3 py-2 w-full text-sm text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-md transition-colors font-medium">
            <LogOut size={16} />
            <span>Sign Out</span>
          </button>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 flex flex-col relative overflow-hidden">
        <header className="h-16 border-b border-slate-200 dark:border-slate-800 glass z-30 flex items-center justify-between px-8 shrink-0">
          <div className="flex items-center gap-3">
             <span className="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">Context</span>
             <ChevronRight size={14} className="text-slate-300" />
             <span className="text-xs font-bold text-slate-900 dark:text-white uppercase tracking-widest">{isLearnerMode ? "Student Preview" : "Admin Principal"}</span>
          </div>
          
          <div className="flex items-center gap-5">
            <div className="flex flex-col items-end">
              <span className="text-[9px] font-black text-primary-600 uppercase tracking-tighter italic">Alpha Node</span>
              <span className="text-[10px] font-bold opacity-60">ID: AZ-9928-1X</span>
            </div>
            <div className="w-8 h-8 rounded-full bg-slate-900 dark:bg-white p-0.5 flex items-center justify-center">
               <User size={16} className="text-white dark:text-slate-900" />
            </div>
          </div>
        </header>

        <div className="flex-1 overflow-y-auto p-10">
          <div className="max-w-6xl mx-auto">
            <Outlet />
          </div>
        </div>
      </main>
    </div>
  );
};
