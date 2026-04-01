import React, { useEffect, useRef } from 'react';
import { ShakaPlayerManager, type PlayerConfig } from '../../infrastructure/player/shaka-player-impl';

interface VideoPlayerProps {
  config: PlayerConfig;
  className?: string;
}

export const VideoPlayer: React.FC<VideoPlayerProps> = ({ config, className }) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const playerManagerRef = useRef<ShakaPlayerManager | null>(null);

  useEffect(() => {
    if (!videoRef.current || !containerRef.current) return;

    const initPlayer = async () => {
      playerManagerRef.current = new ShakaPlayerManager();
      try {
        await playerManagerRef.current.initialize(
          videoRef.current!,
          containerRef.current!,
          config
        );
      } catch (err) {
        console.error('Failed to initialize Shaka Player', err);
      }
    };

    initPlayer();

    return () => {
      playerManagerRef.current?.destroy();
    };
  }, [config]);

  return (
    <div ref={containerRef} className={`shaka-container aspect-video w-full bg-black rounded-xl overflow-hidden ${className}`}>
      <video
        ref={videoRef}
        className="w-full h-full"
        poster="/placeholder-poster.jpg"
      />
    </div>
  );
};
