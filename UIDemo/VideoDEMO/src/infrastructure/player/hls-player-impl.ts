import Hls from 'hls.js';

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

    if (Hls.isSupported()) {
      this.hls = new Hls({
        xhrSetup: (xhr: XMLHttpRequest, _url: string) => {
          // Temporarily disabled for the UI demo since the mock CDN (cdn.zadmuslim.cc) 
          // may not be returning Access-Control-Allow-Credentials: true yet.
          // Enable this once the Next.js BFF proxy & Cloudflare Worker are fully deployed.
          // xhr.withCredentials = true;
        },
      });

      this.hls.on(Hls.Events.ERROR, (_event: any, data: any) => {
        if (data.fatal) {
          switch (data.type) {
            case Hls.ErrorTypes.NETWORK_ERROR:
              if (data.response?.code === 410) {
                console.warn('410 Gone encountered, recovering...');
                this.hls?.recoverMediaError();
              } else {
                console.error("fatal network error encountered, try to recover", data);
                this.hls?.startLoad();
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
