'use client';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';

import { use } from 'react';

export default function TenantDashboard({ params }: { params: Promise<{ tenant: string }> }) {
  const resolvedParams = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const [studentId, setStudentId] = useState<string | null>(null);
  
  // Redemption State
  const [redeemCode, setRedeemCode] = useState('');
  const [redeemStatus, setRedeemStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [redeemMessage, setRedeemMessage] = useState('');

  useEffect(() => {
    const id = localStorage.getItem('student_id');
    const token = localStorage.getItem('auth_token');
    
    if (!token || !id) {
      window.location.href = '/login';
    } else {
      setStudentId(id);
    }
  }, [resolvedParams.tenant, router]);

  const { data: dashboard, isLoading, error } = useQuery({
    queryKey: ['dashboard', studentId],
    queryFn: async () => {
      if (!studentId) return null;
      const res = await apiClient.courses.alphaZeroModulesCoursesPresentationEnrollementsDashboardGetStudentDashboardEndpoint(studentId);
      return res.data;
    },
    enabled: !!studentId,
  });

  const handleRedeem = async () => {
    if (!redeemCode || !studentId) return;
    setRedeemStatus('loading');
    setRedeemMessage('');
    try {
      await apiClient.library.alphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeEndpoint({
        rawCode: redeemCode,
      });
      setRedeemStatus('success');
      setRedeemMessage('Code redeemed successfully!');
      setRedeemCode('');
      // Refetch dashboard to show new course
      queryClient.invalidateQueries({ queryKey: ['dashboard', studentId] });
    } catch (err: any) {
      setRedeemStatus('error');
      setRedeemMessage('Invalid or expired code.');
    }
  };

  if (!studentId) return null; // loading auth state

  return (
    <div className="p-8">
      <section className="mb-12 text-center">
        <h2 className="text-3xl font-bold mb-4">Resume your last lesson</h2>
        <button className="bg-[var(--color-primary)] text-white px-6 py-3 rounded shadow hover:opacity-90 transition-opacity">
          Play Video
        </button>
      </section>

      <section className="mb-12">
        <div className="bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg p-6 flex flex-col md:flex-row items-center justify-between shadow-sm">
          <div className="mb-4 md:mb-0">
            <h3 className="text-xl font-bold mb-2">Got a physical code?</h3>
            <p className="text-gray-600 dark:text-gray-400">Redeem it to unlock new courses and library access.</p>
            {redeemMessage && (
              <p className={`mt-2 text-sm ${redeemStatus === 'error' ? 'text-red-500' : 'text-green-500'}`}>
                {redeemMessage}
              </p>
            )}
          </div>
          <div className="flex gap-2 w-full md:w-auto">
            <input 
              type="text" 
              placeholder="Enter your code" 
              value={redeemCode}
              onChange={(e) => setRedeemCode(e.target.value)}
              className="border p-2 rounded focus:ring-2 focus:ring-[var(--color-primary)] focus:outline-none dark:bg-gray-700 dark:border-gray-600 flex-1 md:w-64"
            />
            <button 
              onClick={handleRedeem}
              disabled={redeemStatus === 'loading' || !redeemCode}
              className="bg-[var(--color-secondary)] text-white px-6 py-2 rounded font-semibold hover:opacity-90 transition-opacity disabled:opacity-50"
            >
              {redeemStatus === 'loading' ? 'Verifying...' : 'Redeem Code'}
            </button>
          </div>
        </div>
      </section>

      <section>
        <h3 className="text-2xl font-bold mb-6 border-b pb-2">My Courses</h3>
        
        {isLoading && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[1, 2, 3].map((i) => (
              <div key={i} className="border border-gray-200 rounded shadow-sm overflow-hidden animate-pulse">
                <div className="h-40 bg-gray-200"></div>
                <div className="p-4">
                  <div className="h-6 bg-gray-200 mb-2 w-3/4 rounded"></div>
                  <div className="h-4 bg-gray-200 w-1/2 rounded"></div>
                </div>
              </div>
            ))}
          </div>
        )}

        {error && (
          <div className="text-red-500 bg-red-50 p-4 rounded text-center">
            Could not load courses. <button onClick={() => window.location.reload()} className="underline font-semibold">Retry</button>
          </div>
        )}

        {dashboard?.academies && Object.values(dashboard.academies)[0]?.length === 0 && (
          <div className="text-center py-12 text-gray-500">
            <div className="text-5xl mb-4">📚</div>
            <p className="text-lg">You have no active courses.</p>
          </div>
        )}

        {dashboard?.academies && Object.values(dashboard.academies)[0]?.length > 0 && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {Object.values(dashboard.academies)[0].map((course: any) => (
              <div key={course.courseId} className="border border-gray-200 rounded shadow-sm overflow-hidden flex flex-col cursor-pointer hover:shadow-md transition-shadow" onClick={() => router.push(`/${resolvedParams.tenant}/courses/${course.courseId}`)}>
                <div className="h-40 bg-gray-100 flex items-center justify-center text-gray-400">
                  {/* Course Image Placeholder */}
                  No Image
                </div>
                <div className="p-4 flex-1 flex flex-col">
                  <h4 className="font-bold text-lg mb-2">{course.courseName}</h4>
                  <div className="mt-auto">
                    <div className="w-full bg-gray-200 rounded-full h-2.5 mb-2">
                      <div className="bg-[var(--color-primary)] h-2.5 rounded-full" style={{ width: `${course.progressPercentage}%` }}></div>
                    </div>
                    <span className="text-xs text-gray-500">{course.progressPercentage}% Complete</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
