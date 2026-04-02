import { useEffect, useRef } from 'react';
import { useVideoStore } from '../store/video-store';
import { isFinalState } from '../../shared/utils/status-utils';

export const usePollVideoStatus = (intervalMs: number = 3000) => {
  const { videos, refreshVideoState } = useVideoStore();
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    // Find videos that are NOT in a final state
    const processingVideos = videos.filter((v) => {
      const isFinished = isFinalState(v);
      return !isFinished;
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
