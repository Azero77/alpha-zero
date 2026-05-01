import { apiClient } from './apiClient';
import { config } from '../config';
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
    const course = response.data;
    
    // Normalize item thumbnails in course structure
    if (course.sections) {
      course.sections.forEach((section: any) => {
        if (section.items) {
          section.items.forEach((item: any) => {
            if (item.metadata && item.metadata.ThumbnailUrl) {
              const thumb = item.metadata.ThumbnailUrl;
              item.metadata.ThumbnailUrl = thumb.startsWith('http') 
                ? thumb 
                : `${config.CDN_URL}/${thumb}`;
            }
          });
        }
      });
    }
    
    return course;
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
    await apiClient.patch(`/courses/${courseId}/approve`);
  }

  async publishCourse(courseId: string) {
    await apiClient.patch(`/courses/${courseId}/publish`);
  }

  async rejectCourse(courseId: string, reason: string) {
    await apiClient.patch(`/courses/${courseId}/reject`, { reason });
  }

  async submitForReview(courseId: string) {
    await apiClient.patch(`/courses/${courseId}/review`);
  }

  // --- Videos (Provider: VideoUploading) ---

  async getVideos(): Promise<Video[]> {
    const response = await apiClient.get('/api/video-uploading/debug/videos');
    const items = response.data.items || [];
    return items.map((v: any) => {
      const thumbnailUrl = v.thumbnailUrl 
        ? (v.thumbnailUrl.startsWith('http') ? v.thumbnailUrl : `${config.CDN_URL}/${v.thumbnailUrl}`)
        : null;

      return {
        id: v.id,
        title: v.title,
        description: v.description,
        status: v.status || 'Ready',
        duration: v.duration,
        thumbnailUrl,
        streamingUrl: v.streamingUrl,
        url: v.streamingUrl
      };
    });
  }

  async requestUpload(req: { 
    fileName: string, 
    title: string, 
    targetResourceArn: string, 
    generateCustomThumbnailUrl?: boolean 
  }) {
    const response = await apiClient.post('/api/video-uploading/upload', {
      ...req,
      contentType: 'video/mp4',
      generateCustomThumbnailUrl: req.generateCustomThumbnailUrl ?? false
    });
    return response.data; 
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

  async uploadFile(url: string, file: File, uploadInfo: any, onProgress?: (progress: number) => void) {
    const axios = (await import('axios')).default;
    
    // AWS S3 Presigned URLs require metadata headers to match the signature
    const headers: Record<string, string> = {
      'Content-Type': file.type || 'video/mp4',
    };

    // If it's a thumbnail, the backend might expect specific headers or just the content type
    // Based on src/Modules/VideoUploading/Application/Commands/Upload/RequestUpload.cs:81
    // It adds "IsThumbnail" metadata.
    
    if (uploadInfo.isThumbnail) {
      headers['x-amz-meta-isthumbnail'] = 'true';
    } else {
      headers['x-amz-meta-file-name'] = encodeURIComponent(file.name);
      headers['x-amz-meta-videoid'] = uploadInfo.videoId;
      headers['x-amz-meta-tenantid'] = uploadInfo.tenantId;
      headers['x-amz-meta-title'] = encodeURIComponent(uploadInfo.title || file.name);
      headers['x-amz-meta-description'] = encodeURIComponent(uploadInfo.description || '');
      headers['x-amz-meta-videotranscodingmetehod'] = uploadInfo.transcodingMethod || 'FFMPEG';
      headers['x-amz-meta-videoencryptionmethod'] = uploadInfo.encryptionMethod || 'None';
    }

    await axios.put(url, file, {
      headers,
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
