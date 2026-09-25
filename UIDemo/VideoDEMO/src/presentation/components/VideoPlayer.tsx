import React, { useEffect, useRef, useState, useMemo } from 'react';
import { ShakaPlayerManager, type PlayerConfig } from '../../infrastructure/player/shaka-player-impl';
import './VideoPlayer.css';

interface VideoPlayerProps {
  config: PlayerConfig;
  title?: string;
  description?: string;
  onClose: () => void;
  className?: string;
}

export const VideoPlayer: React.FC<VideoPlayerProps> = ({
  config,
  title,
  description,
  onClose,
  className = ''
}) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const managerRef = useRef<ShakaPlayerManager | null>(null);

  const manifestUrl = useMemo(() => config.manifestUrl, [config.manifestUrl]);

  useEffect(() => {
    let isMounted = true;
    
    const initPlayer = async () => {
      if (!videoRef.current || !containerRef.current) return;
      
      try {
        setIsLoading(true);
        setError(null);
        
        if (managerRef.current) {
          await managerRef.current.destroy();
        }
        
        managerRef.current = new ShakaPlayerManager();
        await managerRef.current.initialize(videoRef.current, containerRef.current, config);
        
        if (isMounted) {
          setIsLoading(false);
        }
      } catch (err) {
        if (isMounted) {
          setError('Failed to load video player.');
          setIsLoading(false);
          console.error('Player initialization failed:', err);
        }
      }
    };

    initPlayer();

    return () => {
      isMounted = false;
      if (managerRef.current) {
        managerRef.current.destroy();
        managerRef.current = null;
      }
    };
  }, [manifestUrl, config]);

  return (
    <section className={`video-player-section flex flex-col gap-4 w-full ${className}`}>
      <div className="flex justify-between items-start">
        <div>
          {title && <h2 className="text-2xl font-bold text-slate-900">{title}</h2>}
          {description && <p className="text-slate-500 mt-1">{description}</p>}
        </div>
        <button 
          onClick={onClose}
          className="text-sm font-medium text-slate-500 hover:text-slate-900 transition-colors duration-150 ease-out"
        >
          Close Player
        </button>
      </div>

      <div 
        ref={containerRef}
        className="relative w-full aspect-video bg-black rounded-xl overflow-hidden shaka-container"
      >
        <video
          ref={videoRef}
          className="w-full h-full"
          autoPlay
        />
        
        {isLoading && (
          <div className="absolute inset-0 flex items-center justify-center bg-black/50 text-white z-10">
            <span className="text-sm">Loading player...</span>
          </div>
        )}
        
        {error && (
          <div className="absolute inset-0 flex items-center justify-center bg-black/80 text-red-500 z-10">
            <span className="text-sm font-medium">{error}</span>
          </div>
        )}
      </div>
    </section>
  );
};
