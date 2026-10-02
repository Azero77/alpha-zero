import React from 'react';
import type { Video } from '../../../domain/models/video';
import type { VideoProgressUpdate } from '../../../domain/models/video-state';
import { VideoCard } from '../../components/VideoCard';

interface VideoGridProps {
  videos: Video[];
  videoProgress: Record<string, VideoProgressUpdate>;
  onPlay: (video: Video) => void;
  onDelete: (id: string) => void;
  onInspectSaga?: (video: Video) => void;
  isLoading?: boolean;
}

export const VideoGrid: React.FC<VideoGridProps> = ({
  videos,
  videoProgress,
  onPlay,
  onDelete,
  onInspectSaga,
  isLoading,
}) => {
  if (isLoading && videos.length === 0) {
    return (
      <div className="flex items-center justify-center py-24 text-slate-400">
        <div className="flex items-center gap-3">
          <div className="w-4 h-4 border-2 border-slate-300 border-t-slate-600 rounded-full animate-spin" />
          Loading videos…
        </div>
      </div>
    );
  }

  if (videos.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-slate-400">
        <p className="text-lg font-medium text-slate-500">No videos yet</p>
        <p className="text-sm mt-1">Upload a video to get started</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      {videos.map((video) => (
        <VideoCard
          key={video.id}
          video={video}
          progress={videoProgress[video.id]}
          onPlay={onPlay}
          onDelete={onDelete}
          onInspectSaga={onInspectSaga}
        />
      ))}
    </div>
  );
};
