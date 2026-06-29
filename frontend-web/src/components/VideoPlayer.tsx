'use client';

import React, { useRef, useEffect } from 'react';
import shaka from 'shaka-player/dist/shaka-player.ui';
import 'shaka-player/dist/controls.css';

interface VideoPlayerProps {
  manifestUri: string;
  onProgress?: (currentTime: number) => void;
  startSecond?: number;
  onEnded?: () => void;
}

export default function VideoPlayer({ manifestUri, onProgress, startSecond = 0, onEnded }: VideoPlayerProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const videoContainerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let player: any = null;
    let ui: any = null;

    const initApp = async () => {
      // Install built-in polyfills
      shaka.polyfill.installAll();

      if (shaka.Player.isBrowserSupported()) {
        const video = videoRef.current;
        const videoContainer = videoContainerRef.current;

        if (video && videoContainer) {
          player = new shaka.Player(video);
          ui = new shaka.ui.Overlay(player, videoContainer, video);

          // Listen for errors
          player.addEventListener('error', (event: any) => {
            console.error('Error code', event.detail.code, 'object', event.detail);
          });

          try {
            await player.load(manifestUri, startSecond);
            console.log('The video has now been loaded!');
          } catch (e) {
            console.error('Error loading video', e);
          }
        }
      } else {
        console.error('Browser not supported!');
      }
    };

    initApp();

    return () => {
      if (ui) {
        ui.destroy();
      }
      if (player) {
        player.destroy();
      }
    };
  }, [manifestUri, startSecond]);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    const handleTimeUpdate = () => {
      if (onProgress) {
        onProgress(video.currentTime);
      }
    };

    const handleEnded = () => {
      if (onEnded) {
        onEnded();
      }
    };

    video.addEventListener('timeupdate', handleTimeUpdate);
    video.addEventListener('ended', handleEnded);

    return () => {
      video.removeEventListener('timeupdate', handleTimeUpdate);
      video.removeEventListener('ended', handleEnded);
    };
  }, [onProgress, onEnded]);

  return (
    <div ref={videoContainerRef} className="mx-auto w-full max-w-4xl shadow-lg relative">
      <video ref={videoRef} className="w-full h-full" poster="" />
    </div>
  );
}
