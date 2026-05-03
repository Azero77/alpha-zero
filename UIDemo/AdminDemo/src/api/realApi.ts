import { apiClient } from './apiClient';
import type { 
  Course, 
  Video, 
  Quiz, 
  CreateCourseRequest, 
  AddAssessmentRequest, 
  AddLessonRequest 
} from '../types';

export class RealApiService {
  // --- Courses ---
  
  async getCourses(): Promise<Course[]> {
    try {
      const response = await apiClient.get('/courses');
      return response.data.items || [];
    } catch (e) {
      console.warn('GET /courses failed, falling back to empty list', e);
      return [];
    }
  }

  async getCourse(id: string): Promise<Course> {
    const response = await apiClient.get(`/courses/${id}`);
    return response.data;
  }

  async createCourse(req: CreateCourseRequest): Promise<Course> {
    const response = await apiClient.post('/courses', req);
    return response.data;
  }

  async getSubjects(): Promise<any[]> {
    try {
      const response = await apiClient.get('/courses/subjects?page=1&perPage=100');
      return response.data.items || [];
    } catch {
      return [];
    }
  }

  async createSubject(req: { name: string, description?: string }): Promise<any> {
    const response = await apiClient.post('/courses/subjects', req);
    return response.data;
  }

  async addSection(courseId: string, title: string) {
    const response = await apiClient.post(`/courses/${courseId}/sections`, { title });
    return response.data;
  }

  async addLesson(courseId: string, sectionId: string, req: AddLessonRequest) {
    const response = await apiClient.post(`/courses/${courseId}/sections/${sectionId}/lessons`, req);
    return response.data;
  }

  async addAssessment(courseId: string, sectionId: string, req: AddAssessmentRequest) {
    const response = await apiClient.post(`/courses/${courseId}/sections/${sectionId}/assessments`, req);
    return response.data;
  }

  async reorderSections(courseId: string, sectionIds: string[]) {
    await apiClient.post(`/courses/${courseId}/sections/reorder`, { sectionIds });
  }

  async reorderItems(courseId: string, sectionId: string, itemIds: string[]) {
    await apiClient.post(`/courses/${courseId}/sections/${sectionId}/reorder`, { itemIds });
  }

  async approveCourse(courseId: string) {
    await apiClient.post(`/courses/${courseId}/approve`, {});
  }

  async publishCourse(courseId: string) {
    await apiClient.post(`/courses/${courseId}/publish`, {});
  }

  async rejectCourse(courseId: string, reason: string) {
    await apiClient.post(`/courses/${courseId}/reject`, { reason });
  }

  async submitForReview(courseId: string) {
    await apiClient.post(`/courses/${courseId}/review`, {});
  }

  // --- Videos (Provider: VideoUploading) ---

  async getVideos(): Promise<Video[]> {
    const response = await apiClient.get('/api/video-uploading/debug/videos');
    const items = response.data.items || [];
    return items.map((v: any) => ({
      id: v.id,
      title: v.title,
      description: v.description,
      status: v.status || 'Ready',
      duration: v.duration,
      thumbnailUrl: v.thumbnailUrl,
      streamingUrl: v.streamingUrl,
      url: v.streamingUrl
    }));
  }

  async requestUpload(req: { 
    fileName: string, 
    title: string, 
    description?: string,
    targetResourceArn: string, 
    generateCustomThumbnailUrl?: boolean 
  }) {
    const response = await apiClient.post('/api/video-uploading/upload', {
      ...req,
      contentType: 'video/mp4',
      generateCustomThumbnailUrl: req.generateCustomThumbnailUrl ?? false
    });
    // Merge original request params with response so metadata is available for S3 upload
    return { ...response.data, ...req }; 
  }

  async getStreamingInfo(id: string): Promise<any> {
    const response = await apiClient.get(`/api/video/${id}`);
    return response.data;
  }

  async getVideoState(id: string): Promise<any> {
    const response = await apiClient.get(`/api/video-uploading/debug/videos/${id}/state`);
    return response.data;
  }

  async deleteVideo(id: string): Promise<void> {
    await apiClient.delete(`/api/video-uploading/debug/videos/${id}`);
  }

  async uploadFile(url: string, file: File, headers: Record<string, string>, onProgress?: (progress: number) => void) {
    const axios = (await import('axios')).default;
    
    // S3 is extremely case-sensitive. Ensure headers object is healthy.
    if (!headers) {
      console.error('[uploadFile] CRITICAL: No headers provided for S3 upload. S3 will return 403.', { url, file });
      throw new Error('Upload failed: Missing required security headers from backend.');
    }

    const finalHeaders = { ...headers };
    
    // Ensure content-type is correctly set if it was provided in any case
    const contentType = headers['content-type'] || headers['Content-Type'];
    if (contentType) {
      finalHeaders['Content-Type'] = contentType;
    }

    await axios.put(url, file, {
      headers: finalHeaders,
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
          onProgress(progress);
        }
      },
    });
  }

  // --- Assessments (Provider: Assessments) ---

  async getQuizzes(): Promise<Quiz[]> {
    try {
      const response = await apiClient.get('/assessments');
      const items = response.data.items || response.data || [];
      const reverseTypeMap: Record<number, string> = { 0: 'MCQ', 1: 'Handwritten', 2: 'Hybrid' };
      return items.map((q: any) => ({
        ...q,
        type: typeof q.type === 'number' ? reverseTypeMap[q.type] : q.type
      }));
    } catch {
      return [];
    }
  }

  async createQuiz(quiz: Omit<Quiz, 'id'>): Promise<Quiz> {
    const typeMap: Record<string, number> = { 'MCQ': 0, 'Handwritten': 1, 'Hybrid': 2 };
    const response = await apiClient.post('/assessments', {
      ...quiz,
      type: typeMap[quiz.type as string] ?? 0
    });
    return response.data;
  }

  async getAssessment(id: string): Promise<Quiz> {
    const response = await apiClient.get(`/assessments/${id}`);
    const data = response.data;
    const reverseTypeMap: Record<number, string> = { 0: 'MCQ', 1: 'Handwritten', 2: 'Hybrid' };
    return {
      ...data,
      type: typeof data.type === 'number' ? reverseTypeMap[data.type] : data.type
    };
  }

  async updateAssessmentContent(id: string, content: any): Promise<void> {
    await apiClient.put(`/assessments/${id}/content`, { content });
  }
}
