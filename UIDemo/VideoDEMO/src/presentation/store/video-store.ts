import { create } from 'zustand';
import type { Video } from '../../domain/models/video';
import type { IVideoRepository } from '../../domain/repositories/video-repository';
import { VideoRepositoryImpl } from '../../infrastructure/api/video-repository-impl';

interface VideoState {
  videos: Video[];
  totalCount: number;
  currentPage: number;
  isLoading: boolean;
  error: string | null;
  
  fetchVideos: (page?: number, perPage?: number) => Promise<void>;
  deleteVideo: (id: string) => Promise<void>;
  refreshVideoState: (id: string) => Promise<void>;
}

const videoRepository: IVideoRepository = new VideoRepositoryImpl();

export const useVideoStore = create<VideoState>((set, get) => ({
  videos: [],
  totalCount: 0,
  currentPage: 1,
  isLoading: false,
  error: null,

  fetchVideos: async (page = 1, perPage = 10) => {
    set({ isLoading: true, error: null });
    try {
      const result = await videoRepository.getVideos(page, perPage);
      set({ 
        videos: result.items, 
        totalCount: result.totalCount, 
        currentPage: page, 
        isLoading: false 
      });
    } catch (err: any) {
      set({ error: err.message || 'Failed to fetch videos', isLoading: false });
    }
  },

  deleteVideo: async (id: string) => {
    try {
      await videoRepository.deleteVideo(id);
      const { currentPage } = get();
      await get().fetchVideos(currentPage);
    } catch (err: any) {
      set({ error: err.message || 'Failed to delete video' });
    }
  },

  refreshVideoState: async (id: string) => {
    try {
      const state = await videoRepository.getVideoState(id);
      const isPublished = state.currentState.toLowerCase() === 'published';
      const isFailed = state.isFailed || state.currentState.toLowerCase() === 'failed';

      set((s) => ({
        videos: s.videos.map((v) =>
          v.id === id ? { 
            ...v, 
            sagaState: state.currentState, 
            status: isPublished ? 'Published' : (isFailed ? 'Failed' : v.status) 
          } : v
        ),
      }));
      
      // If it just finished, full refresh to get final URLs
      if (isPublished) {
        await get().fetchVideos(get().currentPage);
      }
    } catch (err) {
      console.error('Failed to refresh state for', id, err);
    }
  },
}));
