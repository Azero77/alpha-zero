import React, { useEffect, useState, useCallback } from 'react';
import { Upload, Video, RefreshCcw, ChevronLeft, ChevronRight, Activity, X, Cpu } from 'lucide-react';
import { useVideoStore } from '../store/video-store';
import { usePollVideoStatus } from '../hooks/usePollVideoStatus';
import { VideoGrid } from '../features/dashboard/VideoGrid';
import { UploadModal } from '../features/upload/UploadModal';
import { PipelineTestModal } from '../features/test/PipelineTestModal';
import { VideoPlayer } from '../components/VideoPlayer';
import { normalizeVideoStatus } from '../../shared/utils/status-utils';
import type { Video as VideoType } from '../../domain/models/video';
import type { VideoState } from '../../domain/models/video-state';
import type { PlayerConfig } from '../../infrastructure/player/shaka-player-impl';
import { VideoRepositoryImpl } from '../../infrastructure/api/video-repository-impl';
import { config } from '../../core/config';

const videoRepo = new VideoRepositoryImpl();

export const DashboardPage: React.FC = () => {
  const {
    videos,
    totalCount,
    currentPage,
    isLoading,
    error,
    videoProgress,
    fetchVideos,
  } = useVideoStore();

  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);
  const [activePlayer, setActivePlayer] = useState<{
    video: VideoType;
    config: PlayerConfig;
  } | null>(null);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);
  const [inspectedSaga, setInspectedSaga] = useState<{ video: VideoType; state: VideoState | null; loading: boolean } | null>(null);
  const [isTestModalOpen, setIsTestModalOpen] = useState(false);

  // Real-time progress via SignalR (falls back to polling)
  usePollVideoStatus();

  const perPage = 12;
  const totalPages = Math.ceil(totalCount / perPage);

  useEffect(() => {
    fetchVideos(1, perPage);
  }, [fetchVideos]);

  const processingCount = videos.filter(
    (v: VideoType) => normalizeVideoStatus(v) === 'Processing'
  ).length;

  // ── Player ──────────────────────────────────────────────

  const handlePlay = useCallback(async (video: VideoType) => {
    try {
      const streamingInfo = await videoRepo.getStreamingInfo(video.id);
      const playerConfig: PlayerConfig = {
        manifestUrl: streamingInfo.url,
        posterUrl: video.thumbnailUrl || undefined,
      };

      // ClearKey encryption — fetch raw binary key
      if (streamingInfo.encryptionMethod === 'ClearKey') {
        const keyUrl = `${config.streamingApiUrl}/keys/${video.id}`;
        const headers: Record<string, string> = {};
        if (config.tenantId) headers['X-TenantId'] = config.tenantId;
        if (config.authToken) headers['Authorization'] = `Bearer ${config.authToken}`;
        const keyResponse = await fetch(keyUrl, { headers });
        const keyBuffer = await keyResponse.arrayBuffer();
        const keyHex = Array.from(new Uint8Array(keyBuffer))
          .map((b) => b.toString(16).padStart(2, '0'))
          .join('');
        playerConfig.clearKey = { key: keyHex };
      }

      // Widevine/PlayReady DRM
      if (streamingInfo.drm?.widevineUrl || streamingInfo.drm?.playReadyUrl) {
        playerConfig.drm = streamingInfo.drm;
      }

      setActivePlayer({ video, config: playerConfig });
    } catch (err) {
      console.error('Failed to initialize playback:', err);
    }
  }, []);

  // ── Inspect Pipeline Saga ───────────────────────────────

  const handleInspectSaga = useCallback(async (video: VideoType) => {
    setInspectedSaga({ video, state: null, loading: true });
    try {
      const state = await videoRepo.getVideoState(video.id);
      setInspectedSaga({ video, state, loading: false });
    } catch (err) {
      console.error('Failed to fetch saga state:', err);
      setInspectedSaga({ video, state: null, loading: false });
    }
  }, []);

  // ── Delete ──────────────────────────────────────────────

  const handleDelete = useCallback((id: string) => {
    setDeleteConfirmId(id);
  }, []);

  const confirmDelete = useCallback(async () => {
    if (!deleteConfirmId) return;
    await useVideoStore.getState().deleteVideo(deleteConfirmId);
    setDeleteConfirmId(null);
    if (activePlayer?.video.id === deleteConfirmId) {
      setActivePlayer(null);
    }
  }, [deleteConfirmId, activePlayer]);

  // ── Pagination ──────────────────────────────────────────

  const goToPage = useCallback(
    (page: number) => {
      if (page >= 1 && page <= totalPages) {
        fetchVideos(page, perPage);
      }
    },
    [fetchVideos, totalPages]
  );

  return (
    <div className="min-h-screen bg-[#FAFAFA]">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 sticky top-0 z-30">
        <div className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-slate-900 text-white rounded-lg">
              <Video size={20} />
            </div>
            <div>
              <h1 className="text-lg font-bold text-slate-900 tracking-tight">
                AlphaZero Video Pipeline
              </h1>
              <p className="text-[11px] text-slate-500">
                S3 &rarr; Step Functions &rarr; Transcoder &rarr; R2 &rarr; SQS &rarr; SignalR
              </p>
            </div>
            {processingCount > 0 && (
              <span className="ml-2 bg-amber-100 text-amber-700 text-xs font-semibold px-2 py-0.5 rounded-full flex items-center gap-1.5">
                <span className="w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse" />
                {processingCount} processing
              </span>
            )}
          </div>

          <div className="flex items-center gap-2.5">
            <button
              onClick={() => fetchVideos(currentPage, perPage)}
              className="p-2 text-slate-500 hover:text-slate-800 hover:bg-slate-100 rounded-lg transition-colors"
              title="Refresh videos"
            >
              <RefreshCcw size={16} />
            </button>
            <button
              onClick={() => setIsTestModalOpen(true)}
              className="flex items-center gap-1.5 border border-slate-300 bg-white text-slate-700 px-3 py-2 text-xs font-semibold rounded-lg hover:bg-slate-50 transition-colors shadow-sm"
              title="Test AWS CDK Ingestion Pipeline Contracts"
            >
              <Cpu size={14} className="text-slate-500" />
              Test Ingestion
            </button>
            <button
              onClick={() => setIsUploadModalOpen(true)}
              className="flex items-center gap-2 bg-slate-900 text-white px-4 py-2 text-xs font-semibold rounded-lg hover:bg-slate-800 transition-colors shadow-sm"
            >
              <Upload size={14} />
              Upload Asset
            </button>
          </div>
        </div>
      </header>

      {/* Upload Modal (with thumbnail, engine selector, and progress) */}
      <UploadModal
        isOpen={isUploadModalOpen}
        onClose={() => setIsUploadModalOpen(false)}
      />

      {/* Pipeline Test Modal (for testing VideoPipelineConstruct contracts) */}
      <PipelineTestModal
        isOpen={isTestModalOpen}
        onClose={() => setIsTestModalOpen(false)}
        sampleVideoId={videos[0]?.id}
      />

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-6 py-6">
        {/* Active Player (inline, pushes grid down) */}
        {activePlayer && (
          <div className="mb-8 p-6 bg-white border border-slate-200 rounded-2xl shadow-sm">
            <VideoPlayer
              config={activePlayer.config}
              title={activePlayer.video.title}
              description={activePlayer.video.description}
              onClose={() => setActivePlayer(null)}
            />
          </div>
        )}

        {/* Error Banner */}
        {error && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-xl text-xs text-red-700 flex items-center justify-between">
            <span>{error}</span>
            <button onClick={() => fetchVideos(currentPage, perPage)} className="underline font-semibold ml-4">
              Retry
            </button>
          </div>
        )}

        {/* Delete Confirmation */}
        {deleteConfirmId && (
          <div className="mb-6 p-4 bg-white border border-red-200 shadow-sm rounded-xl flex items-center justify-between">
            <span className="text-xs font-medium text-slate-700">
              Delete this video asset? This will remove records from the VideoUploading module.
            </span>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setDeleteConfirmId(null)}
                className="px-3 py-1.5 text-xs text-slate-600 hover:bg-slate-100 rounded-lg transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={confirmDelete}
                className="px-3.5 py-1.5 text-xs font-semibold text-white bg-red-600 hover:bg-red-700 rounded-lg transition-colors"
              >
                Confirm Delete
              </button>
            </div>
          </div>
        )}

        {/* Video Grid */}
        <VideoGrid
          videos={videos}
          videoProgress={videoProgress}
          onPlay={handlePlay}
          onDelete={handleDelete}
          onInspectSaga={handleInspectSaga}
          isLoading={isLoading}
        />

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="mt-8 flex items-center justify-center gap-3">
            <button
              onClick={() => goToPage(currentPage - 1)}
              disabled={currentPage <= 1}
              className="p-2 text-slate-500 hover:text-slate-800 disabled:opacity-30 disabled:cursor-not-allowed transition-colors rounded-lg hover:bg-slate-100"
            >
              <ChevronLeft size={16} />
            </button>
            <span className="text-xs font-medium text-slate-600">
              Page {currentPage} of {totalPages}
            </span>
            <button
              onClick={() => goToPage(currentPage + 1)}
              disabled={currentPage >= totalPages}
              className="p-2 text-slate-500 hover:text-slate-800 disabled:opacity-30 disabled:cursor-not-allowed transition-colors rounded-lg hover:bg-slate-100"
            >
              <ChevronRight size={16} />
            </button>
          </div>
        )}

        {/* Total count */}
        <div className="mt-4 text-center text-xs text-slate-400">
          {totalCount} total video{totalCount !== 1 ? 's' : ''} in pipeline
        </div>
      </main>

      {/* Saga State Inspection Drawer / Modal */}
      {inspectedSaga && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-slate-200">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2">
                <Activity size={18} className="text-slate-700" />
                <h3 className="text-sm font-bold text-slate-900">Saga State: {inspectedSaga.video.title}</h3>
              </div>
              <button onClick={() => setInspectedSaga(null)} className="text-slate-400 hover:text-slate-700">
                <X size={16} />
              </button>
            </div>
            <div className="py-4">
              {inspectedSaga.loading ? (
                <div className="text-center text-xs text-slate-400 py-6">Loading saga execution details...</div>
              ) : inspectedSaga.state ? (
                <pre className="text-[11px] bg-slate-900 text-emerald-400 p-4 rounded-xl overflow-x-auto font-mono max-h-72">
                  {JSON.stringify(inspectedSaga.state, null, 2)}
                </pre>
              ) : (
                <div className="text-center text-xs text-slate-500 py-6">No active saga state found for this video.</div>
              )}
            </div>
            <div className="flex justify-end">
              <button
                onClick={() => setInspectedSaga(null)}
                className="px-4 py-1.5 text-xs bg-slate-100 hover:bg-slate-200 rounded-lg text-slate-700"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
