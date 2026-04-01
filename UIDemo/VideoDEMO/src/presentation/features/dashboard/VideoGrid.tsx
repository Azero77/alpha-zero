import React from 'react';
import { useVideoStore } from '../../store/video-store';
import { VideoCard } from '../../components/VideoCard';
import type { Video } from '../../../domain/models/video';
import { AnimatePresence } from 'framer-motion';
import { Inbox } from 'lucide-react';

interface VideoGridProps {
  onPlay: (video: Video) => void;
}

export const VideoGrid: React.FC<VideoGridProps> = ({ onPlay }) => {
  const { videos, isLoading, deleteVideo } = useVideoStore();

  if (isLoading && videos.length === 0) {
    return (
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
        {[...Array(8)].map((_, i) => (
          <div key={i} className="card h-64 animate-pulse bg-slate-100" />
        ))}
      </div>
    );
  }

  if (videos.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-slate-400">
        <div className="bg-slate-100 p-6 rounded-full mb-4">
          <Inbox size={48} />
        </div>
        <p className="text-lg font-medium">No videos found</p>
        <p className="text-sm">Try uploading your first video!</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
      <AnimatePresence mode="popLayout">
        {videos.map((video) => (
          <VideoCard
            key={video.id}
            video={video}
            onPlay={onPlay}
            onDelete={deleteVideo}
          />
        ))}
      </AnimatePresence>
    </div>
  );
};
