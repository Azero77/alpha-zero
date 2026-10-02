import * as signalR from '@microsoft/signalr';
import { config } from '../../core/config';
import type { VideoProgressUpdate } from '../../domain/models/video-state';

class VideoProgressClient {
  private connection: signalR.HubConnection | null = null;
  private progressCallback: ((update: VideoProgressUpdate) => void) | null = null;
  private isConnecting: boolean = false;

  constructor() {
    this.buildConnection();
  }

  private buildConnection() {
    if (!config.signalRHubUrl) {
      console.warn('SignalR Hub URL is not configured.');
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(config.signalRHubUrl, {
        skipNegotiation: false,
        accessTokenFactory: config.authToken ? () => config.authToken! : undefined,
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.connection.on('ProgressUpdated', (update: VideoProgressUpdate) => {
      if (this.progressCallback) {
        this.progressCallback(update);
      }
    });

    this.connection.onreconnecting(error => {
      console.log(`SignalR reconnecting: ${error}`);
    });

    this.connection.onreconnected(connectionId => {
      console.log(`SignalR reconnected. Connection ID: ${connectionId}`);
    });

    this.connection.onclose(error => {
      console.log(`SignalR connection closed: ${error}`);
    });
  }

  public async start(): Promise<void> {
    if (!this.connection) return;
    if (this.connection.state !== signalR.HubConnectionState.Disconnected) return;
    if (this.isConnecting) return;

    try {
      this.isConnecting = true;
      await this.connection.start();
      console.log('SignalR Connected');
    } catch (err) {
      console.error('SignalR Connection Error: ', err);
      throw err;
    } finally {
      this.isConnecting = false;
    }
  }

  public async stop(): Promise<void> {
    if (!this.connection) return;
    if (this.connection.state === signalR.HubConnectionState.Disconnected) return;
    
    try {
      await this.connection.stop();
      console.log('SignalR Disconnected');
    } catch (err) {
      console.error('SignalR Disconnect Error: ', err);
    }
  }

  public async joinVideoGroup(videoId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) return;
    
    try {
      await this.connection.invoke('JoinVideoGroup', videoId);
    } catch (err) {
      console.error(`Error joining video group for ${videoId}: `, err);
    }
  }

  public async leaveVideoGroup(videoId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) return;
    
    try {
      await this.connection.invoke('LeaveVideoGroup', videoId);
    } catch (err) {
      console.error(`Error leaving video group for ${videoId}: `, err);
    }
  }

  public onProgressUpdated(callback: (update: VideoProgressUpdate) => void): void {
    this.progressCallback = callback;
  }

  public offProgressUpdated(): void {
    this.progressCallback = null;
  }

  public get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

export const videoProgressClient = new VideoProgressClient();
