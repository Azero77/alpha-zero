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
      // Extract items from the PagedResult structure
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
    // This uses the Orchestrated Endpoint: POST /courses/{id}/sections/{id}/assessments
    const response = await apiClient.post(`/courses/${courseId}/sections/${sectionId}/assessments`, req);
    return response.data;
  }

  // --- Videos (Provider: VideoUploading) ---

  async getVideos(): Promise<Video[]> {
    const response = await apiClient.get('/api/video-uploading/debug/videos');
    // Map backend DTO from PagedResult.items to our frontend Video type
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
    return response.data; // videoId, preSignedUrl, etc.
  }

  async uploadFile(url: string, file: File, onProgress?: (progress: number) => void) {
    const axios = (await import('axios')).default;
    await axios.put(url, file, {
      headers: {
        'Content-Type': file.type || 'video/mp4',
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
    // If no global list exists, we might need a different strategy, 
    // but usually there's a way to see all quizzes for a tenant.
    try {
      const response = await apiClient.get('/assessments');
      return response.data;
    } catch {
      return [];
    }
  }

  async createQuiz(quiz: Omit<Quiz, 'id'>): Promise<Quiz> {
    const response = await apiClient.post('/assessments', quiz);
    return response.data;
  }
}
