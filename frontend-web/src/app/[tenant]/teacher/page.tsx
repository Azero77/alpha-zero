'use client';
import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { useParams, useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';

export default function TeacherDashboard() {
  const params = useParams() as { tenant: string };
  const router = useRouter();

  // Assuming teacher login stores a specific role or just using the auth token
  useEffect(() => {
    const token = localStorage.getItem('auth_token');
    if (!token) {
      router.push(`/${params.tenant}/login`);
    } else {
      apiClient.instance.interceptors.request.use((config) => {
        config.headers.Authorization = `Bearer ${token}`;
        return config;
      });
    }
  }, [params.tenant, router]);

  return (
    <div className="p-8 max-w-6xl mx-auto">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-4xl font-bold text-[var(--color-primary)]">Teacher Dashboard</h1>
        <button className="bg-[var(--color-secondary)] text-white px-6 py-2 rounded shadow hover:opacity-90">
          Generate New Codes
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-12">
        <div className="bg-white dark:bg-gray-800 p-6 rounded shadow-sm border border-gray-100 dark:border-gray-700">
          <h3 className="text-gray-500 text-sm font-semibold mb-1">Total Students</h3>
          <p className="text-3xl font-bold">1,204</p>
        </div>
        <div className="bg-white dark:bg-gray-800 p-6 rounded shadow-sm border border-gray-100 dark:border-gray-700">
          <h3 className="text-gray-500 text-sm font-semibold mb-1">Active Courses</h3>
          <p className="text-3xl font-bold">8</p>
        </div>
        <div className="bg-white dark:bg-gray-800 p-6 rounded shadow-sm border border-gray-100 dark:border-gray-700">
          <h3 className="text-gray-500 text-sm font-semibold mb-1">Codes Redeemed</h3>
          <p className="text-3xl font-bold">4,520</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <section>
          <h2 className="text-2xl font-bold mb-4 border-b pb-2">Recent Enrollments</h2>
          <div className="bg-white dark:bg-gray-800 rounded shadow-sm border border-gray-100 dark:border-gray-700 overflow-hidden">
            <table className="w-full text-left">
              <thead className="bg-gray-50 dark:bg-gray-700">
                <tr>
                  <th className="p-4 font-semibold text-gray-600 dark:text-gray-300">Student</th>
                  <th className="p-4 font-semibold text-gray-600 dark:text-gray-300">Course</th>
                  <th className="p-4 font-semibold text-gray-600 dark:text-gray-300">Date</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                {[1, 2, 3, 4, 5].map((i) => (
                  <tr key={i} className="hover:bg-gray-50 dark:hover:bg-gray-700">
                    <td className="p-4">Student {i}</td>
                    <td className="p-4">Physics 101</td>
                    <td className="p-4 text-gray-500 text-sm">Today</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-bold mb-4 border-b pb-2">Batch Code Generation</h2>
          <div className="bg-white dark:bg-gray-800 rounded shadow-sm border border-gray-100 dark:border-gray-700 p-6">
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Generate new physical library codes for offline distribution.
            </p>
            <form className="space-y-4">
              <div>
                <label className="block text-sm font-semibold mb-1">Select Course</label>
                <select className="w-full border p-2 rounded dark:bg-gray-700 dark:border-gray-600">
                  <option>Physics 101</option>
                  <option>Math 202</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-semibold mb-1">Number of Codes</label>
                <input type="number" defaultValue={50} className="w-full border p-2 rounded dark:bg-gray-700 dark:border-gray-600" />
              </div>
              <button type="button" className="w-full bg-[var(--color-primary)] text-white font-bold py-3 rounded hover:opacity-90 transition">
                Generate CSV
              </button>
            </form>
          </div>
        </section>
      </div>
    </div>
  );
}
