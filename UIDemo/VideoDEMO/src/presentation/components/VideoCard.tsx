import React from 'react';
import { motion } from 'framer-motion';
import type { Video } from '../../domain/models/video';
import { Play, Trash2, Clock, CheckCircle, AlertCircle, Loader2 } from 'lucide-react';
import { clsx } from 'clsx';

interface VideoCardProps {
  video: Video;
  onPlay: (video: Video) => void;
  onDelete: (id: string) => void;
}

const statusConfig = {
  Pending: { icon: Clock, color: 'text-slate-400', bg: 'bg-slate-100', label: 'Pending', spin: false },
  Analyzing: { icon: Loader2, color: 'text-blue-500', bg: 'bg-blue-50', label: 'Analyzing', spin: true },
  Transcoding: { icon: Loader2, color: 'text-primary-500', bg: 'bg-primary-50', label: 'Processing', spin: true },
  Distributing: { icon: Loader2, color: 'text-indigo-500', bg: 'bg-indigo-50', label: 'Distributing', spin: true },
  Published: { icon: CheckCircle, color: 'text-green-500', bg: 'bg-green-50', label: 'Live', spin: false },
  Failed: { icon: AlertCircle, color: 'text-red-500', bg: 'bg-red-50', label: 'Failed', spin: false },
};

export const VideoCard: React.FC<VideoCardProps> = ({ video, onPlay, onDelete }) => {
  const status = statusConfig[video.status] || statusConfig.Pending;
  const StatusIcon = status.icon;

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, scale: 0.95 }}
      whileHover={{ y: -4 }}
      className="card group relative flex flex-col"
    >
      {/* Thumbnail Placeholder */}
      <div className="aspect-video w-full bg-slate-100 relative group-hover:bg-slate-200 transition-colors">
        <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
          {video.status === 'Published' && (
            <button
              onClick={() => onPlay(video)}
              className="bg-white p-3 rounded-full shadow-lg text-primary-600 hover:scale-110 transition-transform"
            >
              <Play fill="currentColor" size={24} />
            </button>
          )}
        </div>
        
        {/* Status Badge */}
        <div className={clsx(
          "absolute top-3 left-3 px-2 py-1 rounded-md text-xs font-semibold flex items-center gap-1.5 shadow-sm",
          status.bg,
          status.color
        )}>
          <StatusIcon size={14} className={status.spin ? "animate-spin" : ""} />
          {status.label}
        </div>
      </div>

      <div className="p-4 flex-1 flex flex-col">
        <h3 className="font-semibold text-slate-800 line-clamp-1 group-hover:text-primary-600 transition-colors">
          {video.title}
        </h3>
        <p className="text-slate-500 text-sm mt-1 line-clamp-2">
          {video.description || 'No description provided.'}
        </p>
        
        <div className="mt-auto pt-4 flex items-center justify-between text-xs text-slate-400">
          <span>{new Date(video.createdOn).toLocaleDateString()}</span>
          <button
            onClick={() => onDelete(video.id)}
            className="p-1.5 text-slate-400 hover:text-red-500 hover:bg-red-50 rounded-md transition-all"
          >
            <Trash2 size={16} />
          </button>
        </div>
      </div>
    </motion.div>
  );
};
