import React, { useEffect, useState, useMemo } from 'react';
import { useVideoStore } from '../store/video-store';
import { VideoGrid } from '../features/dashboard/VideoGrid';
import { UploadModal } from '../features/upload/UploadModal';
import { VideoPlayer } from '../components/VideoPlayer';
import type { Video } from '../../domain/models/video';
import { Plus, Video as VideoIcon, LayoutDashboard, Settings, LogOut, ChevronRight } from 'lucide-react';
import { clsx } from 'clsx';
import { VideoRepositoryImpl } from '../../infrastructure/api/video-repository-impl';
import type { StreamingInfo } from '../../domain/repositories/video-repository';
import { usePollVideoStatus } from '../hooks/usePollVideoStatus';
import { config } from '../../core/config';

const videoRepo = new VideoRepositoryImpl();

export const DashboardPage: React.FC = () => {
  const { fetchVideos } = useVideoStore();
  const [isUploadOpen, setIsUploadOpen] = useState(false);
  const [selectedVideo, setSelectedVideo] = useState<Video | null>(null);
  const [streamingInfo, setStreamingInfo] = useState<StreamingInfo | null>(null);
  const [activeTab, setActiveTab] = useState('dashboard');

  // Use polling hook
  usePollVideoStatus();

  useEffect(() => {
    fetchVideos();
  }, [fetchVideos]);

  const handlePlay = async (video: Video) => {
    try {
      const info = await videoRepo.getStreamingInfo(video.id);
      setStreamingInfo(info);
      setSelectedVideo(video);
    } catch (err) {
      alert('Failed to get streaming info');
    }
  };

  const playerConfig = useMemo(() => {
    if (!streamingInfo || !selectedVideo) return null;
    
    const posterUrl = selectedVideo.thumbnailUrl 
      ? (selectedVideo.thumbnailUrl.startsWith('http') ? selectedVideo.thumbnailUrl : `${config.cdnUrl}/${selectedVideo.thumbnailUrl}`)
      : undefined;

    return {
      manifestUrl: streamingInfo.url,
      posterUrl: posterUrl,
      // If the backend sends DRM details (Premium video)
      drm: streamingInfo.drm ? {
        widevineUrl: streamingInfo.drm.widevineUrl,
        playReadyUrl: streamingInfo.drm.playReadyUrl,
        token: streamingInfo.drm.token
      } : undefined,
      // If the backend sends a raw key (Free video)
      clearKey: streamingInfo.key ? {
        keyId: streamingInfo.key,
        key: streamingInfo.key
      } : undefined
    };
  }, [streamingInfo]);

  return (
    <div className="flex min-h-screen bg-slate-50">
      {/* Sidebar */}
      <aside className="w-64 bg-white border-r border-slate-200 flex flex-col hidden lg:flex">
        <div className="p-6 flex items-center gap-3">
          <div className="bg-primary-600 p-2 rounded-lg text-white">
            <VideoIcon size={24} />
          </div>
          <span className="text-xl font-bold tracking-tight">AlphaZero</span>
        </div>

        <nav className="flex-1 px-4 py-4 space-y-1">
          <button 
            onClick={() => setActiveTab('dashboard')}
            className={clsx(
              "flex items-center gap-3 px-4 py-3 w-full rounded-xl font-medium transition-all text-left outline-none",
              activeTab === 'dashboard' ? "bg-primary-50 text-primary-700" : "text-slate-500 hover:bg-slate-50"
            )}
          >
            <LayoutDashboard size={20} />
            Dashboard
          </button>
          <button 
            onClick={() => setActiveTab('library')}
            className={clsx(
              "flex items-center gap-3 px-4 py-3 w-full rounded-xl font-medium transition-all text-left outline-none",
              activeTab === 'library' ? "bg-primary-50 text-primary-700" : "text-slate-500 hover:bg-slate-50"
            )}
          >
            <VideoIcon size={20} />
            My Library
          </button>
          <button 
            onClick={() => setActiveTab('settings')}
            className={clsx(
              "flex items-center gap-3 px-4 py-3 w-full rounded-xl font-medium transition-all text-left outline-none",
              activeTab === 'settings' ? "bg-primary-50 text-primary-700" : "text-slate-500 hover:bg-slate-50"
            )}
          >
            <Settings size={20} />
            Settings
          </button>
        </nav>

        <div className="p-4 border-t border-slate-100">
          <button className="flex items-center gap-3 px-4 py-3 w-full text-slate-500 hover:text-red-600 hover:bg-red-50 rounded-xl font-medium transition-colors">
            <LogOut size={20} />
            Logout
          </button>
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 flex flex-col overflow-hidden">
        {/* Header */}
        <header className="h-20 bg-white border-b border-slate-200 px-8 flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm text-slate-400">
            <span>Pages</span>
            <ChevronRight size={14} />
            <span className="text-slate-900 font-medium">Dashboard</span>
          </div>

          <button 
            onClick={() => setIsUploadOpen(true)}
            className="btn btn-primary gap-2"
          >
            <Plus size={18} />
            Upload New Video
          </button>
        </header>

        {/* Scrollable Area */}
        <div className="flex-1 overflow-y-auto p-8">
          {selectedVideo && streamingInfo && (
            <div className="mb-12 animate-slide-up">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h2 className="text-2xl font-bold text-slate-900">{selectedVideo.title}</h2>
                  <p className="text-slate-500">{selectedVideo.description}</p>
                </div>
                <button 
                  onClick={() => { setSelectedVideo(null); setStreamingInfo(null); }}
                  className="text-primary-600 font-semibold hover:underline"
                >
                  Close Player
                </button>
              </div>
              <VideoPlayer 
                config={playerConfig!} 
              />
            </div>
          )}

          <div className="mb-6 flex items-center justify-between">
            <h2 className="text-xl font-bold text-slate-900">Your Library</h2>
            <div className="flex gap-2">
              <span className="text-sm text-slate-500">
                Sorted by: <span className="font-medium text-slate-900 cursor-pointer">Recently added</span>
              </span>
            </div>
          </div>

          <VideoGrid onPlay={handlePlay} />
        </div>
      </main>

      <UploadModal 
        isOpen={isUploadOpen} 
        onClose={() => setIsUploadOpen(false)} 
      />
    </div>
  );
};
