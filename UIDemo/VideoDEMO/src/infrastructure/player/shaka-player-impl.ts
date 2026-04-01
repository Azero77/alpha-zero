import shaka from 'shaka-player/dist/shaka-player.ui.js';
import 'shaka-player/dist/controls.css';

export interface PlayerConfig {
  manifestUrl: string;
  clearKey?: {
    keyId: string;
    key: string;
  };
}

export class ShakaPlayerManager {
  private player: shaka.Player | null = null;
  private ui: any = null;

  async initialize(videoElement: HTMLVideoElement, containerElement: HTMLElement, config: PlayerConfig) {
    // Install polyfills
    shaka.polyfill.installAll();

    if (!shaka.Player.isBrowserSupported()) {
      throw new Error('Browser not supported for Shaka Player');
    }

    this.player = new shaka.Player(videoElement);
    
    // Set up UI
    this.ui = new shaka.ui.Overlay(this.player, containerElement, videoElement);
    
    // Configure DRM if ClearKey is provided
    if (config.clearKey) {
      this.player.configure({
        drm: {
          clearKeys: {
            [config.clearKey.keyId]: config.clearKey.key,
          },
        },
      });
    }

    // ABR configuration for low bandwidth
    this.player.configure({
      abr: {
        enabled: true,
        defaultBandwidthEstimate: 500000, // 500kbps baseline
      },
    });

    try {
      await this.player.load(config.manifestUrl);
    } catch (e) {
      console.error('Error loading manifest', e);
      throw e;
    }
  }

  destroy() {
    if (this.player) {
      this.player.destroy();
    }
    if (this.ui) {
      this.ui.destroy();
    }
  }

  getVariantTracks() {
    return this.player?.getVariantTracks() || [];
  }

  selectTrack(track: shaka.extern.Track) {
    this.player?.selectVariantTrack(track, true);
  }
}
