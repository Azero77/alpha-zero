import { useEffect, useRef } from 'react';
import { useVideoStore } from '../store/video-store';
import { videoProgressClient } from '../../infrastructure/signalr/video-progress-client';
import { isFinalState } from '../../shared/utils/status-utils';

export const usePollVideoStatus = (intervalMs: number = 5000) => {
  const { videos, refreshVideoState, updateVideoProgress, fetchVideos } = useVideoStore();
  const fallbackIntervalRef = useRef<number | null>(null);

  useEffect(() => {
    // Get processing videos (not final state)
    const processingVideos = videos.filter(v => !isFinalState(v.status));
    
    if (processingVideos.length === 0) {
      // Disconnect and clear fallback if no videos are processing
      videoProgressClient.stop();
      if (fallbackIntervalRef.current) {
        window.clearInterval(fallbackIntervalRef.current);
        fallbackIntervalRef.current = null;
      }
      return;
    }

    let isSubscribed = true;

    const setupSignalR = async () => {
      videoProgressClient.onProgressUpdated((update) => {
        if (!isSubscribed) return;
        updateVideoProgress(update);
        
        // If it's reached final state and published, fetch the full videos array
        if (update.status === 'COMPLETE' && update.stage === 'publishing') {
           fetchVideos(); // re-fetch to get final published state
        }
      });

      try {
        await videoProgressClient.start();
        if (isSubscribed) {
          // Join groups for each processing video
          for (const video of processingVideos) {
            await videoProgressClient.joinVideoGroup(video.id);
          }
        }
      } catch (err) {
        console.error('SignalR setup failed, falling back to polling', err);
        // Fallback polling setup
        if (!fallbackIntervalRef.current && isSubscribed) {
          fallbackIntervalRef.current = window.setInterval(() => {
            const currentProcessingVideos = useVideoStore.getState().videos.filter(v => !isFinalState(v.status));
            currentProcessingVideos.forEach(v => {
              refreshVideoState(v.id);
            });
          }, intervalMs);
        }
      }
    };

    setupSignalR();

    return () => {
      isSubscribed = false;
      videoProgressClient.offProgressUpdated();
      
      // Leave groups
      if (videoProgressClient.isConnected) {
        processingVideos.forEach(v => {
          videoProgressClient.leaveVideoGroup(v.id).catch(console.error);
        });
      }
      
      if (fallbackIntervalRef.current) {
        window.clearInterval(fallbackIntervalRef.current);
        fallbackIntervalRef.current = null;
      }
    };
  }, [videos, refreshVideoState, updateVideoProgress, fetchVideos, intervalMs]);
};
