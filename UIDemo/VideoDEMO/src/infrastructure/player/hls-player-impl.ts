import Hls from 'hls.js';
import { config } from '../../core/config';

export interface PlayerConfig {
  manifestUrl: string;
  posterUrl?: string;
}

export class HlsPlayerManager {
  private hls: Hls | null = null;
  private videoElement: HTMLVideoElement | null = null;

  async initialize(videoElement: HTMLVideoElement, _containerElement: HTMLElement, config: PlayerConfig): Promise<void> {
    this.videoElement = videoElement;

    if (config.posterUrl) {
      videoElement.poster = config.posterUrl;
    }

    let networkErrorCount = 0;

    if (Hls.isSupported()) {
      this.hls = new Hls({
        xhrSetup: (xhr: XMLHttpRequest, url: string) => {
          // Attach credentials to our backend API to identify the user
          if (url.includes('/api/video/keys')) {
            if (config.tenantId) {
              xhr.setRequestHeader('X-TenantId', config.tenantId);
            }
            if (config.authToken) {
              xhr.setRequestHeader('Authorization', `Bearer ${config.authToken}`);
            }
          }
        },
      });

      this.hls.on(Hls.Events.ERROR, (_event: any, data: any) => {
        if (data.fatal) {
          switch (data.type) {
            case Hls.ErrorTypes.NETWORK_ERROR:
              if (data.response?.code === 410) {
                console.warn('410 Gone encountered, recovering...');
                this.hls?.recoverMediaError();
              } else if (networkErrorCount < 3) {
                networkErrorCount++;
                console.warn(`fatal network error encountered, try to recover (attempt ${networkErrorCount})`, data);
                this.hls?.startLoad();
              } else {
                console.error("fatal network error, max retries reached. destroying player.", data);
                this.hls?.destroy();
              }
              break;
            case Hls.ErrorTypes.MEDIA_ERROR:
              console.error("fatal media error encountered, try to recover", data);
              this.hls?.recoverMediaError();
              break;
            default:
              console.error("fatal error, cannot recover", data);
              this.hls?.destroy();
              break;
          }
        }
      });

      this.hls.attachMedia(videoElement);
      
      return new Promise((resolve) => {
        this.hls!.on(Hls.Events.MEDIA_ATTACHED, () => {
          this.hls!.loadSource(config.manifestUrl);
          resolve();
        });
      });
    }
    else if (videoElement.canPlayType('application/vnd.apple.mpegurl')) {
      videoElement.src = config.manifestUrl;
      return Promise.resolve();
    } else {
      throw new Error('HLS is not supported in this browser.');
    }
  }

  async destroy(): Promise<void> {
    if (this.hls) {
      this.hls.destroy();
      this.hls = null;
    }
    if (this.videoElement) {
      this.videoElement.removeAttribute('src');
      this.videoElement.load();
      this.videoElement = null;
    }
  }
}
