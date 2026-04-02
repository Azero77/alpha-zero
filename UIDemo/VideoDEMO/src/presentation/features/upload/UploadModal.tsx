import React, { useState, useCallback } from 'react';
import { useDropzone } from 'react-dropzone';
import { motion } from 'framer-motion';
import { Upload, X, CheckCircle2, AlertCircle } from 'lucide-react';
import { UploadVideoUseCase } from '../../../application/use-cases/upload-video-use-case';
import { VideoRepositoryImpl } from '../../../infrastructure/api/video-repository-impl';
import { useVideoStore } from '../../store/video-store';

interface UploadModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const uploadUseCase = new UploadVideoUseCase(new VideoRepositoryImpl());

export const UploadModal: React.FC<UploadModalProps> = ({ isOpen, onClose }) => {
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [progress, setProgress] = useState(0);
  const [status, setStatus] = useState<'idle' | 'uploading' | 'success' | 'error'>('idle');
  const [errorMessage, setError] = useState('');
  const fetchVideos = useVideoStore(s => s.fetchVideos);

  const onDrop = useCallback((acceptedFiles: File[]) => {
    if (acceptedFiles[0]) {
      setFile(acceptedFiles[0]);
      setTitle(acceptedFiles[0].name.replace(/\.[^/.]+$/, ""));
      setStatus('idle');
    }
  }, []);

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: { 'video/mp4': ['.mp4'] },
    multiple: false,
  });

  const handleUpload = async () => {
    if (!file || !title.trim()) return;

    setStatus('uploading');
    setProgress(0);

    try {
      await uploadUseCase.execute(file, title, description, (p) => setProgress(p));
      setStatus('success');
      setTimeout(() => {
        fetchVideos();
        handleClose();
      }, 2000);
    } catch (err: any) {
      setStatus('error');
      setError(err.message || 'Upload failed');
    }
  };

  const handleClose = () => {
    setFile(null);
    setTitle('');
    setDescription('');
    setProgress(0);
    setStatus('idle');
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/40 backdrop-blur-sm">
      <motion.div
        initial={{ opacity: 0, scale: 0.95, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.95, y: 20 }}
        className="bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden"
      >
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <h2 className="text-xl font-bold text-slate-800">Upload Video</h2>
          <button onClick={handleClose} className="text-slate-400 hover:text-slate-600 transition-colors">
            <X size={24} />
          </button>
        </div>

        <div className="p-8">
          {status === 'idle' && (
            <div className="space-y-6">
              <div
                {...getRootProps()}
                className={`border-2 border-dashed rounded-xl p-8 flex flex-col items-center justify-center transition-all cursor-pointer ${
                  isDragActive ? 'border-primary-500 bg-primary-50' : 'border-slate-200 hover:border-primary-400 hover:bg-slate-50'
                }`}
              >
                <input {...getInputProps()} />
                <div className="bg-primary-100 p-3 rounded-full text-primary-600 mb-3">
                  <Upload size={24} />
                </div>
                <p className="text-sm font-semibold text-slate-700">
                  {file ? file.name : 'Drag & drop video here'}
                </p>
                <p className="text-xs text-slate-500 mt-1">MP4 files only (max 500MB)</p>
              </div>

              {file && (
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Video Title</label>
                    <input
                      type="text"
                      value={title}
                      onChange={(e) => setTitle(e.target.value)}
                      placeholder="Enter a descriptive title"
                      className="w-full px-4 py-2 rounded-lg border border-slate-200 focus:border-primary-500 focus:ring-2 focus:ring-primary-200 transition-all outline-none"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Description (Optional)</label>
                    <textarea
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                      placeholder="What is this video about?"
                      rows={3}
                      className="w-full px-4 py-2 rounded-lg border border-slate-200 focus:border-primary-500 focus:ring-2 focus:ring-primary-200 transition-all outline-none resize-none"
                    />
                  </div>
                </div>
              )}
            </div>
          )}

          {(status === 'uploading' || status === 'success' || status === 'error') && (
            <div className="flex flex-col items-center py-8">
              {status === 'uploading' && (
                <>
                  <div className="relative w-24 h-24 mb-6">
                    <svg className="w-full h-full" viewBox="0 0 100 100">
                      <circle className="text-slate-100 stroke-current" strokeWidth="8" fill="transparent" r="40" cx="50" cy="50" />
                      <motion.circle
                        className="text-primary-600 stroke-current"
                        strokeWidth="8"
                        strokeLinecap="round"
                        fill="transparent"
                        r="40" cx="50" cy="50"
                        initial={{ strokeDasharray: "251.2", strokeDashoffset: 251.2 }}
                        animate={{ strokeDashoffset: 251.2 - (251.2 * progress) / 100 }}
                      />
                    </svg>
                    <div className="absolute inset-0 flex items-center justify-center font-bold text-slate-700">
                      {progress}%
                    </div>
                  </div>
                  <p className="text-lg font-medium text-slate-700">Uploading your video...</p>
                </>
              )}

              {status === 'success' && (
                <motion.div initial={{ scale: 0.5 }} animate={{ scale: 1 }} className="flex flex-col items-center">
                  <div className="bg-green-100 p-4 rounded-full text-green-600 mb-4">
                    <CheckCircle2 size={48} />
                  </div>
                  <p className="text-lg font-bold text-slate-800">Upload Complete!</p>
                  <p className="text-sm text-slate-500 mt-1 text-center">We're now processing your video for optimal streaming.</p>
                </motion.div>
              )}

              {status === 'error' && (
                <div className="flex flex-col items-center text-center">
                  <div className="bg-red-100 p-4 rounded-full text-red-600 mb-4">
                    <AlertCircle size={48} />
                  </div>
                  <p className="text-lg font-bold text-slate-800">Something went wrong</p>
                  <p className="text-sm text-slate-500 mt-1">{errorMessage}</p>
                  <button onClick={() => setStatus('idle')} className="mt-6 btn btn-primary">Try Again</button>
                </div>
              )}
            </div>
          )}
        </div>

        {status === 'idle' && (
          <div className="px-8 pb-8 flex gap-3">
            <button
              disabled={!file}
              onClick={handleUpload}
              className="flex-1 btn btn-primary py-3"
            >
              Start Upload
            </button>
          </div>
        )}
      </motion.div>
    </div>
  );
};
