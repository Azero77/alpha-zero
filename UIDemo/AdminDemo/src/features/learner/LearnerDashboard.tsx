import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, Clock, Award, PlayCircle, ArrowRight } from 'lucide-react';
import { api } from '../../api';
import { Link } from 'react-router-dom';

export const LearnerDashboard: React.FC = () => {
  const { data: courses, isLoading } = useQuery({
    queryKey: ['courses'],
    queryFn: () => api.getCourses()
  });

  return (
    <div className="space-y-12 animate-in fade-in duration-700">
      <div className="space-y-2">
        <h1 className="text-4xl font-black tracking-tight text-slate-900 dark:text-white">Your Learning Path</h1>
        <p className="text-slate-500 font-medium">Continue your journey across the AlphaZero network.</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
        {isLoading ? (
          [1, 2, 3].map(i => <div key={i} className="h-64 bg-slate-100 dark:bg-slate-900 rounded-2xl animate-pulse" />)
        ) : courses?.map(course => (
          <Link 
            to={`/learn/${course.id}`} 
            key={course.id}
            className="group relative bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-2xl overflow-hidden hover:shadow-2xl transition-all duration-500 flex flex-col h-full"
          >
            {/* Visual Header */}
            <div className="h-32 bg-slate-100 dark:bg-slate-900 relative overflow-hidden">
               <div className="absolute inset-0 bg-gradient-to-br from-primary-600/10 to-transparent" />
               <div className="absolute inset-0 flex items-center justify-center opacity-20 group-hover:scale-110 transition-transform duration-700">
                 <BookOpen size={64} className="text-slate-400" />
               </div>
               <div className="absolute bottom-4 left-6 px-3 py-1 bg-white/90 dark:bg-slate-950/90 backdrop-blur-sm rounded-full border border-slate-200/50 dark:border-slate-800/50 text-[10px] font-black uppercase tracking-widest text-slate-600">
                 {course.status}
               </div>
            </div>

            <div className="p-8 flex-1 flex flex-col">
              <h3 className="text-xl font-bold mb-3 group-hover:text-primary-600 transition-colors leading-tight">
                {course.title}
              </h3>
              <p className="text-sm text-slate-500 line-clamp-2 mb-8 flex-1 leading-relaxed">
                {course.description || "Start your fundamental training in this academy."}
              </p>

              <div className="flex items-center justify-between pt-6 border-t border-slate-100 dark:border-slate-900">
                <div className="flex items-center gap-4">
                  <div className="flex items-center gap-1.5 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                    <PlayCircle size={14} className="text-primary-500" />
                    {course.sections.reduce((acc, s) => acc + s.items.length, 0)} Items
                  </div>
                  <div className="flex items-center gap-1.5 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                    <Clock size={14} />
                    Auto
                  </div>
                </div>
                <ArrowRight size={18} className="text-slate-300 group-hover:text-primary-600 group-hover:translate-x-1 transition-all" />
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
};
