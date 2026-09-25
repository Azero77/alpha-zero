import React from 'react';
import type { Video } from '../../domain/models/video';
import type { VideoProgressUpdate } from '../../domain/models/video-state';
import { Play, Trash2, CheckCircle, AlertCircle, Loader2, Video as VideoIcon, Clock, Activity } from 'lucide-react';
import { clsx } from 'clsx';
import { normalizeVideoStatus } from '../../shared/utils/status-utils';
import { config } from '../../core/config';

interface VideoCardProps {
  video: Video;
  progress?: VideoProgressUpdate;
  onPlay: (video: Video) => void;
  onDelete: (id: string) => void;
  onInspectSaga?: (video: Video) => void;
}

const statusConfig = {
  Processing: { icon: Loader2, color: 'text-amber-600', bg: 'bg-amber-50', label: 'Processing', spin: true },
  Published: { icon: CheckCircle, color: 'text-emerald-600', bg: 'bg-emerald-50', label: 'Live', spin: false },
  Failed: { icon: AlertCircle, color: 'text-red-600', bg: 'bg-red-50', label: 'Failed', spin: false },
  Deleted: { icon: Trash2, color: 'text-slate-400', bg: 'bg-slate-100', label: 'Deleted', spin: false },
};

const PIPELINE_STAGES = ['analyzing', 'preparing', 'transcoding', 'publishing'] as const;

function formatDuration(durationStr?: string): string | null {
  if (!durationStr || durationStr === '00:00:00') return null;
  // Parse TimeSpan format "00:05:30" or "00:05:30.1234567"
  const parts = durationStr.split(':');
  if (parts.length < 3) return null;
  const hours = parseInt(parts[0], 10);
  const minutes = parseInt(parts[1], 10);
  const seconds = parseInt(parts[2].split('.')[0], 10);
  if (hours > 0) return `${hours}h ${minutes}m`;
  if (minutes > 0) return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  return `0:${seconds.toString().padStart(2, '0')}`;
}

function getResolutionLabel(height?: number): string | null {
  if (!height || height === 0) return null;
  if (height >= 2160) return '4K';
  if (height >= 1440) return '1440p';
  if (height >= 1080) return '1080p';
  if (height >= 720) return '720p';
  if (height >= 480) return '480p';
  return `${height}p`;
}

export const VideoCard: React.FC<VideoCardProps> = ({
  video,
  progress,
  onPlay,
  onDelete,
  onInspectSaga,
}) => {
  const normalizedStatus = normalizeVideoStatus(video);
  const statusInfo = statusConfig[normalizedStatus] || statusConfig.Processing;
  const StatusIcon = statusInfo.icon;
  const isPlayable = normalizedStatus === 'Published';

  const thumbnailUrl = video.thumbnailUrl
    ? (video.thumbnailUrl.startsWith('http') ? video.thumbnailUrl : `${config.cdnUrl}/${video.thumbnailUrl}`)
    : null;

  const duration = formatDuration(video.specifications?.duration);
  const resolution = getResolutionLabel(video.specifications?.resolution?.height);

  // Determine current pipeline stage index for step indicator
  const currentStageIndex = progress
    ? PIPELINE_STAGES.indexOf(progress.stage as typeof PIPELINE_STAGES[number])
    : -1;

  return (
    <div className="bg-white rounded-xl border border-slate-200 overflow-hidden group hover:border-slate-300 transition-colors">
      {/* Thumbnail / Processing State */}
      <div className="aspect-video w-full bg-slate-50 relative overflow-hidden">
        {thumbnailUrl ? (
          <img
            src={thumbnailUrl}
            alt={video.title}
            className="absolute inset-0 w-full h-full object-cover"
          />
        ) : (
          <div className="absolute inset-0 flex items-center justify-center text-slate-200">
            <VideoIcon size={40} />
          </div>
        )}

        {/* Play overlay for published videos */}
        {isPlayable && (
          <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity bg-slate-900/20">
            <button
              onClick={() => onPlay(video)}
              className="bg-white p-3 rounded-full text-slate-900 hover:scale-105 transition-transform"
            >
              <Play fill="currentColor" size={22} />
            </button>
          </div>
        )}

        {/* Processing pipeline visualization */}
        {normalizedStatus === 'Processing' && progress && (
          <div className="absolute inset-0 bg-slate-900/50 flex flex-col items-center justify-center p-4">
            {/* Stage label */}
            <span className="text-white text-sm font-medium capitalize mb-2">
              {progress.stage}
              {progress.percentage != null && ` — ${progress.percentage}%`}
            </span>

            {/* Step indicator */}
            <div className="flex items-center gap-1 mb-3">
              {PIPELINE_STAGES.map((stage, i) => (
                <React.Fragment key={stage}>
                  <div
                    className={clsx(
                      'w-2 h-2 rounded-full transition-colors',
                      i < currentStageIndex
                        ? 'bg-emerald-400'
                        : i === currentStageIndex
                        ? 'bg-white'
                        : 'bg-white/30'
                    )}
                  />
                  {i < PIPELINE_STAGES.length - 1 && (
                    <div
                      className={clsx(
                        'w-4 h-px transition-colors',
                        i < currentStageIndex ? 'bg-emerald-400' : 'bg-white/30'
                      )}
                    />
                  )}
                </React.Fragment>
              ))}
            </div>

            {/* Thin progress bar */}
            {progress.percentage != null && (
              <div className="w-3/4 h-0.5 bg-white/20 rounded-full overflow-hidden">
                <div
                  className="h-full bg-white rounded-full transition-all duration-300 ease-out"
                  style={{ width: `${progress.percentage}%` }}
                />
              </div>
            )}
          </div>
        )}

        {/* Processing state without SignalR progress */}
        {normalizedStatus === 'Processing' && !progress && (
          <div className="absolute inset-0 bg-slate-900/40 flex items-center justify-center">
            <div className="flex flex-col items-center gap-2">
              <Loader2 size={24} className="text-white animate-spin" />
              <span className="text-white text-xs font-medium">Processing…</span>
            </div>
          </div>
        )}

        {/* Duration badge for published videos */}
        {isPlayable && duration && (
          <div className="absolute bottom-2 right-2 bg-slate-900/80 text-white text-xs font-medium px-1.5 py-0.5 rounded flex items-center gap-1">
            <Clock size={10} />
            {duration}
          </div>
        )}

        {/* Status badge */}
        <div className={clsx(
          'absolute top-2 left-2 px-2 py-0.5 rounded text-xs font-semibold flex items-center gap-1',
          statusInfo.bg,
          statusInfo.color
        )}>
          <StatusIcon size={12} className={statusInfo.spin ? 'animate-spin' : ''} />
          {statusInfo.label}
        </div>
      </div>

      {/* Info */}
      <div className="p-4 flex flex-col">
        <h3 className="font-semibold text-slate-900 text-sm leading-tight line-clamp-1 group-hover:text-slate-700 transition-colors">
          {video.title}
        </h3>

        {video.description && (
          <p className="text-slate-500 text-xs mt-1 line-clamp-2">
            {video.description}
          </p>
        )}

        {/* Metadata row */}
        <div className="mt-3 flex items-center gap-2 text-xs text-slate-400">
          {resolution && (
            <span className="bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded font-medium">
              {resolution}
            </span>
          )}
          {video.metadata?.transcodingMethod && (
            <span className="bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded font-medium">
              {video.metadata.transcodingMethod}
            </span>
          )}
          {video.metadata?.encryptionMethod && video.metadata.encryptionMethod !== 'None' && (
            <span className="bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded font-medium">
              {video.metadata.encryptionMethod}
            </span>
          )}
        </div>

        {/* Footer */}
        <div className="mt-3 pt-3 border-t border-slate-100 flex items-center justify-between text-xs text-slate-400">
          <span>{new Date(video.createdOn).toLocaleString()}</span>
          <div className="flex items-center gap-1">
            {onInspectSaga && (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onInspectSaga(video);
                }}
                title="Inspect Ingestion Pipeline / Saga State"
                className="p-1 text-slate-400 hover:text-slate-800 hover:bg-slate-100 rounded transition-colors"
              >
                <Activity size={14} />
              </button>
            )}
            <button
              onClick={(e) => {
                e.stopPropagation();
                onDelete(video.id);
              }}
              title="Delete Video"
              className="p-1 text-slate-400 hover:text-red-500 hover:bg-red-50 rounded transition-colors"
            >
              <Trash2 size={14} />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
