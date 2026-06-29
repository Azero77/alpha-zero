'use client';
import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { useParams, useRouter } from 'next/navigation';
import { useState, useEffect } from 'react';
import dynamic from 'next/dynamic';
import Quiz from '@/components/Quiz';

// Dynamically import VideoPlayer to avoid SSR issues with Shaka Player
const VideoPlayer = dynamic(() => import('@/components/VideoPlayer'), { ssr: false });

export default function CourseDetails() {
  const params = useParams() as { tenant: string; courseId: string };
  const router = useRouter();
  
  const [activeVideoItem, setActiveVideoItem] = useState<any>(null);
  const [videoProgress, setVideoProgress] = useState<Record<string, number>>({});

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('video_progress');
      if (saved) {
        try {
          setVideoProgress(JSON.parse(saved));
        } catch (e) {}
      }
    }
  }, []);

  const { data: course, isLoading, error } = useQuery({
    queryKey: ['course', params.courseId],
    queryFn: async () => {
      const res = await apiClient.courses.alphaZeroModulesCoursesPresentationCoursesGetCourseGetCourseEndpoint(params.courseId);
      return res.data;
    }
  });

  const handleProgress = (time: number) => {
    if (activeVideoItem?.id) {
      const newProgress = { ...videoProgress, [activeVideoItem.id]: time };
      setVideoProgress(newProgress);
      localStorage.setItem('video_progress', JSON.stringify(newProgress));
    }
  };

  const handleMarkComplete = async () => {
    if (!activeVideoItem?.id) return;
    try {
      const studentId = localStorage.getItem('student_id');
      if (studentId) {
        await apiClient.courses.alphaZeroModulesCoursesPresentationCoursesCompleteItemCompleteItemEndpoint({
          studentId,
          courseId: params.courseId,
          itemId: activeVideoItem.id
        });
        alert('Lesson marked as complete!');
      }
    } catch (e) {
      console.error(e);
    }
  };

  if (isLoading) {
    return <div className="p-8 animate-pulse bg-gray-200 h-96 m-8 rounded"></div>;
  }

  if (error || !course) {
    return (
      <div className="p-8 text-center text-red-500">
        <h2>Failed to load course details.</h2>
        <button onClick={() => router.push(`/${params.tenant}`)}>Go back</button>
      </div>
    );
  }

  // Generate a mock manifest URI if none exists, just for the MVP player
  const manifestUri = activeVideoItem?.resource?.arn || 'https://dash.akamaized.net/akamai/bbb_30fps/bbb_30fps.mpd';
  const startSecond = activeVideoItem?.id ? videoProgress[activeVideoItem.id] || 0 : 0;

  return (
    <div className="p-8 max-w-5xl mx-auto">
      <h1 className="text-4xl font-bold mb-4">{course.title}</h1>
      <p className="text-gray-600 mb-8">{course.description}</p>
      
      <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
        <div className="md:col-span-2">
          {activeVideoItem ? (
            <div className="mb-8">
              {activeVideoItem.type === 'Video' ? (
                <>
                  <VideoPlayer 
                    manifestUri={manifestUri} 
                    onProgress={handleProgress}
                    startSecond={startSecond}
                    onEnded={handleMarkComplete}
                  />
                  <div className="mt-4 flex justify-between items-center">
                    <h2 className="text-xl font-bold">{activeVideoItem.title}</h2>
                    <button 
                      onClick={handleMarkComplete}
                      className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700 transition"
                    >
                      Mark as Complete
                    </button>
                  </div>
                </>
              ) : activeVideoItem.type === 'Quiz' ? (
                <Quiz 
                  courseId={params.courseId} 
                  quizId={activeVideoItem.id} 
                  questions={activeVideoItem.resource?.questions || []} 
                  tenant={params.tenant} 
                />
              ) : (
                <div className="bg-gray-100 p-8 rounded flex items-center justify-center">
                  <p>Unsupported content type: {activeVideoItem.type}</p>
                </div>
              )}
            </div>
          ) : (
            <div className="bg-black aspect-video rounded flex items-center justify-center text-white mb-8">
              Select a video from the syllabus to start learning.
            </div>
          )}
        </div>

        <div>
          <h3 className="text-2xl font-bold mb-4 border-b pb-2">Syllabus</h3>
          {course.sections?.map((section: any) => (
            <div key={section.id} className="mb-4">
              <h4 className="font-semibold text-lg mb-2">{section.title}</h4>
              <ul className="space-y-2">
                {section.items?.map((item: any) => (
                  <li 
                    key={item.id} 
                    onClick={() => {
                      if (item.type === 'Video' || item.type === 'Quiz') setActiveVideoItem(item);
                    }}
                    className={`flex justify-between items-center p-2 rounded cursor-pointer border ${activeVideoItem?.id === item.id ? 'border-[var(--color-primary)] bg-blue-50 dark:bg-blue-900/20' : 'border-transparent hover:border-gray-200 hover:bg-gray-50'}`}
                  >
                    <div className="flex items-center gap-2">
                      <span className="text-xl">{item.type === 'Video' ? '🎥' : '📝'}</span>
                      <span className="text-sm">{item.title}</span>
                    </div>
                    {item.type === 'Video' && (
                      <button className="text-xs text-[var(--color-primary)] font-bold">Play</button>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
