export type ResourceArn = string;

export interface Course {
  id: string;
  title: string;
  description: string | null;
  subjectId: string;
  status: 'Draft' | 'Review' | 'Approved' | 'Published';
  sections: CourseSection[];
}

export interface CourseSection {
  id: string;
  title: string;
  order: number;
  items: CourseItem[];
}

export interface CourseItem {
  id: string;
  title: string;
  type: 'Lesson' | 'Assessment';
  order: number;
  bitIndex: number;
  resourceId: string;
  metadata: Record<string, any>;
}

export interface Video {
  id: string;
  title: string;
  description: string | null;
  status: 'Processing' | 'Ready' | 'Published' | 'Failed' | 'Deleted';
  duration?: string;
  thumbnailUrl?: string;
  streamingUrl?: string;
  url?: string;
}

export interface Quiz {
  id: string;
  title: string;
  description: string | null;
  type: 'MCQ' | 'Handwritten' | 'Hybrid';
  passingScore: number;
  content?: QuizContent;
}

export interface QuizContent {
  version: string;
  items: QuizItem[];
}

export interface QuizItem {
  id: string;
  type: number; // ItemType enum
  renderData: any;
  questionType?: number; // QuestionType enum
  points?: number;
  gradingData?: any;
}

export interface CreateCourseRequest {
  title: string;
  description?: string;
  subjectId: string;
}

export interface AddAssessmentRequest {
  title: string;
  assessmentId: string;
  type: string;
  passingScore: number;
  description?: string;
}

export interface AddLessonRequest {
  title: string;
  videoId: string;
}
