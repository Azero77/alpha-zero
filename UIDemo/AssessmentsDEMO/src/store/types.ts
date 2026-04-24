export type AssessmentType = 'MCQ' | 'Handwritten' | 'Hybrid';
export type ItemType = 'Paragraph' | 'Question';
export type QuestionType = 'MCQ' | 'Handwritten' | 'Voice' | 'Video';
export type AssessmentStatus = 'Draft' | 'Published' | 'Archived';
export type SubmissionStatus = 'InProgress' | 'Submitted' | 'UnderReview' | 'Graded';

export interface Choice {
  id: string;
  renderData: any;
}

export interface GradingData {
  choices?: Choice[];
  correctChoiceId?: string;
  shuffleOptions?: boolean;
  rubric?: string;
  aiHint?: string;
}

export interface AssessmentItem {
  id: string;
  type: ItemType;
  renderData: any;
  questionType?: QuestionType;
  points?: number;
  gradingData?: GradingData;
}

export interface AssessmentContent {
  version: string;
  items: AssessmentItem[];
}

export interface Assessment {
  id: string;
  tenantId: string;
  title: string;
  description?: string;
  type: AssessmentType;
  passingScore: number;
  status: AssessmentStatus;
  currentVersionId?: string;
  versions: AssessmentVersion[];
}

export interface AssessmentVersion {
  id: string;
  assessmentId: string;
  versionNumber: number;
  content: AssessmentContent;
  createdAt: string;
}

export interface AiGradingInfo {
  transcribedText?: string;
  suggestedScore?: number;
  aiFeedback?: string;
  confidence: number;
}

export interface SubmissionItem {
  value?: any;
  mediaUrl?: string;
  score?: number;
  teacherFeedback?: string;
  aiGrading?: AiGradingInfo;
}

export interface AssessmentSubmissionResponses {
  answers: Record<string, any>;
}

export interface AssessmentSubmission {
  id: string;
  tenantId: string;
  assessmentId: string;
  assessmentVersionId: string;
  studentId: string;
  status: SubmissionStatus;
  totalScore?: number;
  submittedAt: string;
  gradedAt?: string;
  responses: AssessmentSubmissionResponses;
}
