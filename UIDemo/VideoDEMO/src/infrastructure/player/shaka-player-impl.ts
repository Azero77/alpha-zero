import shaka from 'shaka-player/dist/shaka-player.ui.js';
import 'shaka-player/dist/controls.css';

export interface PlayerConfig {
  manifestUrl: string;
  posterUrl?: string;
  clearKey?: {
    keyId?: string;
    key: string;
  };
  drm?: {
    widevineUrl?: string;
    playReadyUrl?: string;
    token?: string; 
  };
}

export class ShakaPlayerManager {
  private player: shaka.Player | null = null;
  private ui: any = null;

  async initialize(videoElement: HTMLVideoElement, containerElement: HTMLElement, config: PlayerConfig) {
    shaka.polyfill.installAll();

    if (!shaka.Player.isBrowserSupported()) {
      throw new Error('Browser not supported');
    }

    this.player = new shaka.Player();
    await this.player.attach(videoElement);

    // 1. Setup Request Filter for Authenticated Key Delivery
    this.player.getNetworkingEngine()!.registerRequestFilter((type, request) => {
      // Check if this is a request for a decryption key or a DRM license
      const isLicenseOrKey = type === shaka.net.NetworkingEngine.RequestType.LICENSE;
      
      // If our API is hosting the key, we need to add the auth token
      if (isLicenseOrKey) {
        console.log('[Player] Adding Auth header to key/license request:', request.uris[0]);
        
        // In a real app, you would get this from your auth store (Zustand/Redux)
        const token = localStorage.getItem('auth_token'); 
        if (token) {
          request.headers['Authorization'] = `Bearer ${token}`;
        }
      }
    });

    // 2. Modern Shaka 5.0 Configuration
    const shakaConfig = {
      streaming: {
        lowLatencyMode: false,
      },
      manifest: {
        hls: {
          ignoreTextStreamFailures: true,
        }
      }
    };

    // 3. DRM Strategy
    if (config.drm) {
      console.log('[Player] Initializing in Premium DRM Mode');
      const servers: Record<string, string> = {};
      if (config.drm.widevineUrl) servers['com.widevine.alpha'] = config.drm.widevineUrl;
      if (config.drm.playReadyUrl) servers['com.microsoft.playready'] = config.drm.playReadyUrl;

      this.player.configure({
        drm: {
          servers: servers,
          advanced: {
            'com.widevine.alpha': {
              videoRobustness: 'SW_SECURE_CRYPTO',
              audioRobustness: 'SW_SECURE_CRYPTO',
            }
          }
        }
      });

      if (config.drm.token) {
        this.player.getNetworkingEngine()!.registerRequestFilter((type, request) => {
          if (type === shaka.net.NetworkingEngine.RequestType.LICENSE) {
            request.headers['Authorization'] = `Bearer ${config.drm!.token}`;
          }
        });
      }
    } 
    else if (config.clearKey?.key) {
      console.log('[Player] Initializing in Free/Static Key Mode (AES-128)');
    }

    this.player.configure(shakaConfig);
    this.ui = new shaka.ui.Overlay(this.player, containerElement, videoElement);

    // 4. Production-Ready UI Configuration
    const uiConfig = {
      controlPanelElements: [
        'play_pause',
        'time_and_duration',
        'spacer',
        'mute',
        'volume',
        'quality',
        'playback_rate',
        'fullscreen',
      ],
      // This helps prevent overlapping by explicitly managing the center button
      addBigPlayButton: true,
      // Ensure the seek bar doesn't overlap with buttons
      seekBarColors: {
        base: 'rgba(255, 255, 255, 0.3)',
        buffered: 'rgba(255, 255, 255, 0.5)',
        played: '#0ea5e9', // Primary-500
      }
    };

    this.ui.configure(uiConfig);

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
