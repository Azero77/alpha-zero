import { create } from 'zustand';
import type { Video } from '../../domain/models/video';
import type { VideoProgressUpdate } from '../../domain/models/video-state';
import type { IVideoRepository } from '../../domain/repositories/video-repository';
import { VideoRepositoryImpl } from '../../infrastructure/api/video-repository-impl';

interface VideoStoreState {
  videos: Video[];
  totalCount: number;
  currentPage: number;
  isLoading: boolean;
  error: string | null;
  
  // Real-time progress tracking
  videoProgress: Record<string, VideoProgressUpdate>;
  
  // Actions
  fetchVideos: (page?: number, perPage?: number) => Promise<void>;
  refreshVideoState: (id: string) => Promise<void>;
  deleteVideo: (id: string) => Promise<void>;
  updateVideoInfo: (id: string, title: string, description?: string) => Promise<void>;
  updateVideoProgress: (update: VideoProgressUpdate) => void;
  clearVideoProgress: (videoId: string) => void;
}

const repo: IVideoRepository = new VideoRepositoryImpl();

export const useVideoStore = create<VideoStoreState>((set, get) => ({
  videos: [],
  totalCount: 0,
  currentPage: 1,
  isLoading: false,
  error: null,
  videoProgress: {},

  fetchVideos: async (page = 1, perPage = 10) => {
    set({ isLoading: true, error: null });
    try {
      const result = await repo.getVideos(page, perPage);
      set({ 
        videos: result.items, 
        totalCount: result.totalCount,
        currentPage: page,
        isLoading: false 
      });
    } catch (error) {
      const errMessage = error instanceof Error ? error.message : 'Failed to fetch videos';
      set({ error: errMessage, isLoading: false });
    }
  },

  refreshVideoState: async (id: string) => {
    try {
      const video = await repo.getVideoById(id);
      set((state) => {
        const updatedVideos = state.videos.map(v => v.id === id ? video : v);
        return { videos: updatedVideos };
      });
    } catch (error) {
      console.error(`Failed to refresh video state for ${id}`, error);
    }
  },

  deleteVideo: async (id: string) => {
    try {
      await repo.deleteVideo(id);
      // Re-fetch current page
      await get().fetchVideos(get().currentPage);
    } catch (error) {
      const errMessage = error instanceof Error ? error.message : 'Failed to delete video';
      set({ error: errMessage });
      throw error;
    }
  },

  updateVideoInfo: async (id: string, title: string, description?: string) => {
    try {
      await repo.updateVideoInfo(id, { title, description });
      // Re-fetch current page
      await get().fetchVideos(get().currentPage);
    } catch (error) {
      const errMessage = error instanceof Error ? error.message : 'Failed to update video info';
      set({ error: errMessage });
      throw error;
    }
  },

  updateVideoProgress: (update: VideoProgressUpdate) => {
    set((state) => ({
      videoProgress: {
        ...state.videoProgress,
        [update.videoId]: update
      }
    }));
  },

  clearVideoProgress: (videoId: string) => {
    set((state) => {
      const newProgress = { ...state.videoProgress };
      delete newProgress[videoId];
      return { videoProgress: newProgress };
    });
  }
}));
