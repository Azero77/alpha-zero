'use client';
import { useState } from 'react';
import { apiClient } from '@/api/client';
import { useRouter } from 'next/navigation';

interface QuizProps {
  courseId: string;
  quizId: string;
  questions: any[];
  tenant: string;
}

export default function Quiz({ courseId, quizId, questions, tenant }: QuizProps) {
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const router = useRouter();

  const handleSubmit = async () => {
    setIsSubmitting(true);
    try {
      const studentId = localStorage.getItem('student_id');
      if (studentId) {
        await apiClient.courses.alphaZeroModulesCoursesPresentationCoursesCompleteItemCompleteItemEndpoint({
          studentId,
          courseId,
          itemId: quizId
        });
        alert('Quiz submitted successfully!');
        router.push(`/${tenant}`);
      }
    } catch (e) {
      console.error(e);
      alert('Failed to submit quiz.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="bg-white dark:bg-gray-800 p-6 rounded shadow mt-8">
      <h3 className="text-2xl font-bold mb-6 border-b pb-2">Knowledge Check</h3>
      {questions.map((q, idx) => (
        <div key={q.id} className="mb-6">
          <p className="font-semibold mb-3">{idx + 1}. {q.text}</p>
          <div className="space-y-2">
            {q.options?.map((opt: string) => (
              <label key={opt} className="flex items-center space-x-3 cursor-pointer p-2 hover:bg-gray-50 dark:hover:bg-gray-700 rounded border border-transparent hover:border-gray-200">
                <input 
                  type="radio" 
                  name={`question-${q.id}`} 
                  value={opt}
                  onChange={() => setAnswers(prev => ({ ...prev, [q.id]: opt }))}
                  className="w-4 h-4 text-[var(--color-primary)] bg-gray-100 border-gray-300 focus:ring-[var(--color-primary)]"
                />
                <span className="text-gray-700 dark:text-gray-300">{opt}</span>
              </label>
            ))}
          </div>
        </div>
      ))}
      <button 
        onClick={handleSubmit}
        disabled={isSubmitting || Object.keys(answers).length !== questions.length}
        className="mt-6 bg-[var(--color-primary)] text-white px-6 py-2 rounded font-semibold hover:opacity-90 transition disabled:opacity-50"
      >
        {isSubmitting ? 'Submitting...' : 'Submit Quiz'}
      </button>
    </div>
  );
}
