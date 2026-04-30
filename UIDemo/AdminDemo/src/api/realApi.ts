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
      url: v.streamingUrl
    }));
  }

  async requestUpload(req: { fileName: string, title: string, targetResourceArn: string }) {
    const response = await apiClient.post('/api/video-uploading/upload', {
      ...req,
      contentType: 'video/mp4'
    });
    return response.data; 
  }

  async uploadFile(url: string, file: File, uploadInfo: any, onProgress?: (progress: number) => void) {
    const axios = (await import('axios')).default;
    
    // AWS S3 Presigned URLs require metadata headers to match the signature
    await axios.put(url, file, {
      headers: {
        'Content-Type': file.type || 'video/mp4',
        'x-amz-meta-file-name': encodeURIComponent(file.name),
        'x-amz-meta-videoid': uploadInfo.videoId,
        'x-amz-meta-tenantid': uploadInfo.tenantId,
        'x-amz-meta-title': encodeURIComponent(uploadInfo.title || file.name),
        'x-amz-meta-description': encodeURIComponent(uploadInfo.description || ''),
        'x-amz-meta-videotranscodingmetehod': uploadInfo.transcodingMethod || 'FFMPEG',
        'x-amz-meta-videoencryptionmethod': uploadInfo.encryptionMethod || 'None'
      },
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
