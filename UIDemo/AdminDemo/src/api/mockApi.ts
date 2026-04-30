import type { Course, Video, Quiz, CreateCourseRequest, AddAssessmentRequest, AddLessonRequest } from '../types';

const sleep = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

export class MockApiService {
  private courses: Course[] = [
    {
      id: 'course-1',
      title: 'Introduction to Physics',
      description: 'The fundamentals of classical mechanics.',
      subjectId: 'subject-1',
      status: 'Draft',
      sections: [
        {
          id: 'section-1',
          title: 'Basics of Motion',
          order: 0,
          items: [
            {
              id: 'item-1',
              title: 'What is Motion?',
              type: 'Lesson',
              order: 0,
              bitIndex: 0,
              resourceId: 'video-1',
              metadata: { Status: 'Ready', Duration: '10:20' }
            }
          ]
        }
      ]
    }
  ];

  private videos: Video[] = [
    { id: 'video-1', title: 'What is Motion?', description: 'Intro to mechanics', status: 'Ready', duration: '10:20' },
    { id: 'video-2', title: 'Newtonian Laws', description: 'Force and inertia', status: 'Processing' }
  ];

  private quizzes: Quiz[] = [
    { id: 'quiz-1', title: 'Motion Basics Quiz', description: 'Check your knowledge', type: 'MCQ', passingScore: 80 }
  ];

  async getCourses() {
    await sleep(500);
    return this.courses;
  }

  async getCourse(id: string) {
    await sleep(300);
    return this.courses.find(c => c.id === id);
  }

  async createCourse(req: CreateCourseRequest) {
    await sleep(500);
    const newCourse: Course = {
      id: `course-${Date.now()}`,
      ...req,
      description: req.description || null,
      status: 'Draft',
      sections: []
    };
    this.courses.push(newCourse);
    return newCourse;
  }

  async addSection(courseId: string, title: string) {
    await sleep(300);
    const course = this.courses.find(c => c.id === courseId);
    if (course) {
      const newSection = { id: `section-${Date.now()}`, title, order: course.sections.length, items: [] };
      course.sections.push(newSection);
      return newSection;
    }
  }

  async addLesson(courseId: string, sectionId: string, req: AddLessonRequest) {
    await sleep(300);
    const course = this.courses.find(c => c.id === courseId);
    const section = course?.sections.find(s => s.id === sectionId);
    if (section) {
      const newItem = {
        id: `item-${Date.now()}`,
        title: req.title,
        type: 'Lesson' as const,
        order: section.items.length,
        bitIndex: section.items.length, // Rough simplification
        resourceId: req.videoId,
        metadata: { Status: 'Processing' }
      };
      section.items.push(newItem);
      return newItem;
    }
  }

  async addAssessment(courseId: string, sectionId: string, req: AddAssessmentRequest) {
    await sleep(300);
    const course = this.courses.find(c => c.id === courseId);
    const section = course?.sections.find(s => s.id === sectionId);
    if (section) {
      const newItem = {
        id: `item-${Date.now()}`,
        title: req.title,
        type: 'Assessment' as const,
        order: section.items.length,
        bitIndex: section.items.length,
        resourceId: req.assessmentId,
        metadata: { Type: req.type, PassingScore: req.passingScore, Status: 'Published' }
      };
      section.items.push(newItem);
      return newItem;
    }
  }

  async getVideos() {
    await sleep(400);
    return this.videos;
  }

  async getQuizzes() {
    await sleep(400);
    return this.quizzes;
  }

  async createQuiz(quiz: Omit<Quiz, 'id'>) {
    await sleep(500);
    const newQuiz = { ...quiz, id: `quiz-${Date.now()}` };
    this.quizzes.push(newQuiz);
    return newQuiz;
  }

  async getAssessment(id: string): Promise<Quiz> {
    await sleep(300);
    const q = this.quizzes.find(x => x.id === id);
    if (!q) throw new Error('Not found');
    return q;
  }

  async updateAssessmentContent(id: string, content: any): Promise<void> {
    await sleep(300);
    const q = this.quizzes.find(x => x.id === id);
    if (q) q.content = content;
  }

  async requestUpload(_req: { fileName: string, title: string, targetResourceArn: string }) {

    await sleep(300);
    return {
      videoId: `video-${Date.now()}`,
      preSignedUrl: 'https://mock-s3.com/upload',
      tenantId: 'tenant-1'
    };
  }
}

export const api = new MockApiService();
