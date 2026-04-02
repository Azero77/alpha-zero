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
    
    // Intercept license requests to placeholders to prevent resolution errors
    this.player.getNetworkingEngine()!.registerRequestFilter((type, request) => {
      if (type === shaka.net.NetworkingEngine.RequestType.LICENSE && 
          request.uris.some(uri => uri.includes('alphazero.api') || uri.includes('clearkey.local'))) {
        
        // Tag this request so the response filter knows to mock it
        (request as any).isMockedClearKey = true;
      }
    });

    // Mock successful response for the placeholder URLs
    this.player.getNetworkingEngine()!.registerResponseFilter((type, response) => {
      if ((response as any).originalRequest?.isMockedClearKey) {
        // Return a dummy successful status to satisfy Shaka's internal logic
        response.data = new Uint8Array(0).buffer;
        response.status = 200;
        response.uri = 'data:application/json,{}';
      }
    });

    // Set up UI
    this.ui = new shaka.ui.Overlay(this.player, containerElement, videoElement);
    
    // Configure DRM if ClearKey is provided
    if (config.clearKey) {
      this.player.configure({
        drm: {
          clearKeys: {
            [config.clearKey.keyId]: config.clearKey.key,
          },
          // Tell Shaka to ignore the license servers in the manifest
          // so it doesn't try to fetch from alphazero.api
          ignoreManifestDrmInfo: true,
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
