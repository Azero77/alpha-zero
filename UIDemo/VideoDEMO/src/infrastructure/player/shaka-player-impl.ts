import shaka from 'shaka-player/dist/shaka-player.ui.js';
import 'shaka-player/dist/controls.css';

export interface PlayerConfig {
  manifestUrl: string;
  clearKey?: {
    keyId: string;
    key: string;
  };
}

/**
 * A clean, simplified Shaka Player manager focused on HLS AES-128 playback.
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

    // 1. Handle the Key Interception
    // We replace the fake URL 'clearkey.local' with the actual binary key data.
    this.player.getNetworkingEngine()!.registerRequestFilter((_type, request) => {
      const isKeyRequest = request.uris.some(uri => uri.includes('clearkey.local'));
      
      if (isKeyRequest && config.clearKey?.key) {
        console.log('[Player] Intercepting AES-128 key request...');
        
        // Convert the 32-char hex string to 16 bytes
        const hex = config.clearKey.key.replace(/[^0-9a-f]/gi, '');
        const bytes = new Uint8Array(16);
        for (let i = 0; i < 16; i++) {
          bytes[i] = parseInt(hex.substr(i * 2, 2), 16);
        }

        // Convert to data URI to satisfy the fetch
        const base64 = btoa(String.fromCharCode(...bytes));
        request.uris = [`data:application/octet-stream;base64,${base64}`];
      }
    });

    // 2. Setup UI
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
