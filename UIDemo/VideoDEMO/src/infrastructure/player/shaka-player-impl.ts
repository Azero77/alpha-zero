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
  private ui: shaka.ui.Overlay | null = null;

  async initialize(videoElement: HTMLVideoElement, containerElement: HTMLElement, config: PlayerConfig): Promise<void> {
    try {
      this.player = new shaka.Player(videoElement);
      this.ui = new shaka.ui.Overlay(this.player, containerElement, videoElement);

      const uiConfig = {
        controlPanelElements: [
          'play_pause',
          'time_and_duration',
          'spacer',
          'mute',
          'volume',
          'quality',
          'playback_rate',
          'fullscreen'
        ],
        seekBarColors: {
          base: 'rgba(255, 255, 255, 0.3)',
          buffered: 'rgba(255, 255, 255, 0.54)',
          played: '#10B981',
        },
        addBigPlayButton: true
      };
      
      this.ui.configure(uiConfig);

      this.player.addEventListener('error', this.onErrorEvent.bind(this));

      if (config.drm?.token) {
        this.player.getNetworkingEngine()?.registerRequestFilter((type: shaka.net.NetworkingEngine.RequestType, request: shaka.extern.Request) => {
          if (type === shaka.net.NetworkingEngine.RequestType.LICENSE) {
            request.headers['Authorization'] = `Bearer ${config.drm!.token}`;
          }
        });
      }

      const playerConfig: shaka.extern.PlayerConfiguration = {
        drm: {}
      };

      if (config.drm) {
        if (config.drm.widevineUrl || config.drm.playReadyUrl) {
          playerConfig.drm!.servers = {};
          if (config.drm.widevineUrl) {
            playerConfig.drm!.servers['com.widevine.alpha'] = config.drm.widevineUrl;
          }
          if (config.drm.playReadyUrl) {
            playerConfig.drm!.servers['com.microsoft.playready'] = config.drm.playReadyUrl;
          }
        }
      } else if (config.clearKey?.keyId && config.clearKey?.key) {
        playerConfig.drm!.clearKeys = {
          [config.clearKey.keyId]: config.clearKey.key
        };
      }

      this.player.configure(playerConfig);

      await this.player.load(config.manifestUrl);
      
      if (config.posterUrl) {
        videoElement.poster = config.posterUrl;
      }
      
    } catch (error) {
      this.onError(error as shaka.util.Error);
      throw error;
    }
  }

  private onErrorEvent(event: Event) {
    const customEvent = event as unknown as { detail: shaka.util.Error };
    this.onError(customEvent.detail);
  }

  private onError(error: shaka.util.Error) {
    console.error('Error code', error.code, 'object', error);
  }

  async destroy(): Promise<void> {
    if (this.ui) {
      await this.ui.destroy();
      this.ui = null;
    }
    if (this.player) {
      await this.player.destroy();
      this.player = null;
    }
  }
}
