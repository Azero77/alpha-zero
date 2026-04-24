import { create } from 'zustand';
import { Assessment, AssessmentSubmission, AssessmentType } from './types';

interface AssessmentState {
  assessments: Assessment[];
  submissions: AssessmentSubmission[];
  addAssessment: (assessment: Assessment) => void;
  updateAssessment: (id: string, updates: Partial<Assessment>) => void;
  addSubmission: (submission: AssessmentSubmission) => void;
  updateSubmission: (id: string, updates: Partial<AssessmentSubmission>) => void;
}

export const useAssessmentStore = create<AssessmentState>((set) => ({
  assessments: [
    {
      id: 'assessment-1',
      tenantId: 'tenant-1',
      title: 'Physics Midterm',
      description: 'Covering mechanics and thermodynamics',
      type: 'Hybrid',
      passingScore: 50,
      status: 'Published',
      currentVersionId: 'version-1',
      versions: [
        {
          id: 'version-1',
          assessmentId: 'assessment-1',
          versionNumber: 1,
          createdAt: new Date().toISOString(),
          content: {
            version: '1.0',
            items: [
              {
                id: 'item-1',
                type: 'Paragraph',
                renderData: { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Welcome to the midterm. Good luck!' }] }] }
              },
              {
                id: 'q-1',
                type: 'Question',
                questionType: 'MCQ',
                points: 10,
                renderData: { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'What is the unit of Force?' }] }] },
                gradingData: {
                  choices: [
                    { id: 'opt-1', renderData: { text: 'Newton' } },
                    { id: 'opt-2', renderData: { text: 'Joule' } },
                    { id: 'opt-3', renderData: { text: 'Watt' } }
                  ],
                  correctChoiceId: 'opt-1'
                }
              },
              {
                id: 'q-2',
                type: 'Question',
                questionType: 'Handwritten',
                points: 20,
                renderData: { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Draw and explain the free-body diagram of a block on an incline.' }] }] },
                gradingData: {
                  rubric: 'Check for normal force, gravity components, and friction.'
                }
              }
            ]
          }
        }
      ]
    }
  ],
  submissions: [],
  addAssessment: (assessment) => set((state) => ({ assessments: [...state.assessments, assessment] })),
  updateAssessment: (id, updates) => set((state) => ({
    assessments: state.assessments.map((a) => a.id === id ? { ...a, ...updates } : a)
  })),
  addSubmission: (submission) => set((state) => ({ submissions: [...state.submissions, submission] })),
  updateSubmission: (id, updates) => set((state) => ({
    submissions: state.submissions.map((s) => s.id === id ? { ...s, ...updates } : s)
  }))
}));
