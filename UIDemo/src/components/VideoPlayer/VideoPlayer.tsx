import React, { useEffect, useRef } from 'react';
import shaka from 'shaka-player';
import styles from './VideoPlayer.module.css';

interface VideoPlayerProps {
  manifestUrl: string;
  clearKey?: {
    keyId: string;
    key: string;
  };
}

const VideoPlayer: React.FC<VideoPlayerProps> = ({ manifestUrl, clearKey }) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let player: shaka.Player | null = null;

    const initPlayer = async () => {
      if (!videoRef.current) return;

      // Install polyfills
      shaka.polyfill.installAll();

      if (!shaka.Player.isBrowserSupported()) {
        console.error('Browser not supported!');
        return;
      }

      player = new shaka.Player(videoRef.current);

      // Add a request filter to handle relative paths in the manifest when using the API Proxy
      player.getNetworkingEngine()?.registerRequestFilter((_type, request) => {
        // If the URL is just a filename (no http/https), it's a relative segment request
        if (!request.uris[0].startsWith('http') && manifestUrl.includes('/dev/proxy/')) {
          const proxyBase = manifestUrl.substring(0, manifestUrl.lastIndexOf('/') + 1);
          request.uris[0] = proxyBase + request.uris[0];
        }
      });

      // Listen for errors
      player.addEventListener('error', (event: any) => {
        const error = event.detail;
        console.error('Shaka Player Error:', error.code, error.data, error);
        alert(`Shaka Player Error: ${error.code}. Check console for details.`);
      });

      // Configure ClearKey if provided
      if (clearKey) {
        console.log('Configuring ClearKey:', clearKey);
        player.configure({
          drm: {
            clearKeys: {
              [clearKey.keyId]: clearKey.key
            }
          }
        });
      }

      try {
        console.log('Loading manifest:', manifestUrl);
        await player.load(manifestUrl);
        console.log('The video has now been loaded!');
      } catch (e) {
        console.error('Error loading manifest:', e);
      }
    };

    initPlayer();

    return () => {
      if (player) {
        player.destroy();
      }
    };
  }, [manifestUrl, clearKey]);

  return (
    <div ref={containerRef} className={styles.container}>
      <video
        ref={videoRef}
        className={styles.video}
        controls
        autoPlay
      />
    </div>
  );
};

export default VideoPlayer;
