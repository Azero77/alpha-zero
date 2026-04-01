import { useEffect, useRef } from 'react';
import { useVideoStore } from '../store/video-store';

export const usePollVideoStatus = (intervalMs: number = 3000) => {
  const { videos, refreshVideoState } = useVideoStore();
  const intervalRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    // Find videos that are NOT in a final state (case-insensitive check)
    const processingVideos = videos.filter((v) => {
      const status = v.status?.toString().toLowerCase();
      const saga = v.sagaState?.toString().toLowerCase();
      
      const isFinal = status === 'published' || status === 'failed' || saga === 'published' || saga === 'failed';
      return !isFinal || status === 'processing';
    });

    if (processingVideos.length > 0) {
      if (!intervalRef.current) {
        intervalRef.current = setInterval(() => {
          processingVideos.forEach((v) => refreshVideoState(v.id));
        }, intervalMs);
      }
    } else {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    }

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, [videos, refreshVideoState, intervalMs]);
};
