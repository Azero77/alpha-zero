import shaka from 'shaka-player/dist/shaka-player.ui.js';
import 'shaka-player/dist/controls.css';

export interface PlayerConfig {
  manifestUrl: string;
  clearKey?: {
    keyId?: string;
    key: string;
  };
  drm?: {
    widevineUrl?: string;
    playReadyUrl?: string;
    token?: string; // JWT for the DRM license server
  };
}

/**
 * A hybrid Shaka Player manager focused on handling both
 * free (AES-128 static key) and premium (SPEKE DRM) video playback.
 */
export class ShakaPlayerManager {
  private player: shaka.Player | null = null;
  private ui: any = null;

  async initialize(videoElement: HTMLVideoElement, containerElement: HTMLElement, config: PlayerConfig) {
    shaka.polyfill.installAll();

    if (!shaka.Player.isBrowserSupported()) {
      throw new Error('Browser not supported');
    }

    this.player = new shaka.Player(videoElement);

    // Dynamic Configuration
    const shakaConfig: any = {};

    // --- STRATEGY 1: PREMIUM DRM ---
    if (config.drm) {
      console.log('[Player] Initializing in Premium DRM Mode');
      
      const servers: Record<string, string> = {};
      if (config.drm.widevineUrl) servers['com.widevine.alpha'] = config.drm.widevineUrl;
      if (config.drm.playReadyUrl) servers['com.microsoft.playready'] = config.drm.playReadyUrl;

      shakaConfig.drm = {
        servers: servers,
        advanced: {
          'com.widevine.alpha': {
            videoRobustness: 'SW_SECURE_CRYPTO',
            audioRobustness: 'SW_SECURE_CRYPTO',
          }
        }
      };

      // Add the authorization token to license requests
      if (config.drm.token) {
        this.player.getNetworkingEngine()!.registerRequestFilter((type: any, request: any) => {
          if (type === shaka.net.NetworkingEngine.RequestType.LICENSE) {
            request.headers['Authorization'] = `Bearer ${config.drm!.token}`;
          }
        });
      }
    } 
    // --- STRATEGY 2: FREE / STATIC KEY ---
    else if (config.clearKey?.key) {
      console.log('[Player] Initializing in Free/Static Key Mode');
      
      // We replace the fake URL 'clearkey.local' with the actual binary key data.
      this.player.getNetworkingEngine()!.registerRequestFilter((_type: any, request: any) => {
        const isKeyRequest = request.uris.some((uri: string) => uri.includes('clearkey.local') || uri.includes('alphazero.api'));
        
        if (isKeyRequest && config.clearKey?.key) {
          console.log('[Player] Intercepting AES-128 key request...');
          
          // Convert the 32-char hex string to 16 bytes
          const hex = config.clearKey.key.replace(/[^0-9a-f]/gi, '');
          const bytes = new Uint8Array(16);
          for (let i = 0; i < 16; i++) {
            bytes[i] = parseInt(hex.substr(i * 2, 2), 16);
          }

          // Convert to data URI to satisfy the fetch locally
          const base64 = btoa(String.fromCharCode(...bytes));
          request.uris = [`data:application/octet-stream;base64,${base64}`];
        }
      });
    } else {
       console.log('[Player] Initializing in Unencrypted Mode');
    }

    // Apply configuration
    this.player.configure(shakaConfig);

    // Setup UI
    this.ui = new shaka.ui.Overlay(this.player, containerElement, videoElement);

    try {
      console.log('[Player] Loading manifest:', config.manifestUrl);
      await this.player.load(config.manifestUrl);
      console.log('[Player] Video loaded successfully');
    } catch (error) {
      console.error('[Player] Error loading video:', error);
      throw error;
    }
  }

  destroy() {
    if (this.ui) this.ui.destroy();
    if (this.player) this.player.destroy();
  }
}
