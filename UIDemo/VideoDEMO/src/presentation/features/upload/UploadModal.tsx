import React, { useState, useCallback } from 'react';
import { useDropzone } from 'react-dropzone';
import { motion, AnimatePresence } from 'framer-motion';
import { Upload, X, CheckCircle2, AlertCircle, Image as ImageIcon, Film, Settings2 } from 'lucide-react';
import { UploadVideoUseCase } from '../../../application/use-cases/upload-video-use-case';
import { VideoRepositoryImpl } from '../../../infrastructure/api/video-repository-impl';
import { useVideoStore } from '../../store/video-store';
import { config } from '../../../core/config';
import clsx from 'clsx';

interface UploadModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const uploadUseCase = new UploadVideoUseCase(new VideoRepositoryImpl());

export const UploadModal: React.FC<UploadModalProps> = ({ isOpen, onClose }) => {
  const fetchVideos = useVideoStore((s) => s.fetchVideos);

  const [videoFile, setVideoFile] = useState<File | null>(null);
  const [thumbnailFile, setThumbnailFile] = useState<File | null>(null);
  const [thumbnailPreview, setThumbnailPreview] = useState<string | null>(null);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [transcodingEngine, setTranscodingEngine] = useState('FFMPEG');
  const [encryptionMethod, setEncryptionMethod] = useState('None');
  const [targetResourceArn, setTargetResourceArn] = useState(
    `az:video:${config.tenantId}:video/00000000-0000-0000-0000-000000000001`
  );

  const [status, setStatus] = useState<'idle' | 'uploading' | 'success' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [videoProgress, setVideoProgress] = useState(0);
  const [thumbnailProgress, setThumbnailProgress] = useState(0);

  const onVideoDrop = useCallback((acceptedFiles: File[]) => {
    const file = acceptedFiles[0];
    if (file) {
      setVideoFile(file);
      setTitle(file.name.replace(/\.[^/.]+$/, ''));
      setStatus('idle');
    }
  }, []);

  const onThumbnailDrop = useCallback((acceptedFiles: File[]) => {
    const file = acceptedFiles[0];
    if (file) {
      setThumbnailFile(file);
      const url = URL.createObjectURL(file);
      setThumbnailPreview(url);
    }
  }, []);

  const {
    getRootProps: getVideoRootProps,
    getInputProps: getVideoInputProps,
    isDragActive: isVideoDragActive,
  } = useDropzone({
    onDrop: onVideoDrop,
    accept: { 'video/mp4': ['.mp4'] },
    maxFiles: 1,
    disabled: status === 'uploading',
  });

  const {
    getRootProps: getThumbnailRootProps,
    getInputProps: getThumbnailInputProps,
    isDragActive: isThumbnailDragActive,
  } = useDropzone({
    onDrop: onThumbnailDrop,
    accept: { 'image/jpeg': ['.jpg', '.jpeg'], 'image/webp': ['.webp'], 'image/png': ['.png'] },
    maxFiles: 1,
    disabled: status === 'uploading',
  });

  const handleUpload = async () => {
    if (!videoFile || !title.trim()) return;

    setStatus('uploading');
    setVideoProgress(0);
    setThumbnailProgress(0);
    setErrorMessage('');

    try {
      await uploadUseCase.execute(
        videoFile,
        title.trim(),
        description.trim() || undefined,
        (progress) => setVideoProgress(progress),
        {
          transcodingMethod: transcodingEngine,
          encryptionMethod: encryptionMethod,
          targetResourceArn: targetResourceArn.trim(),
          thumbnailFile: thumbnailFile || undefined,
        }
      );

      if (thumbnailFile) {
        setThumbnailProgress(100);
      }

      setStatus('success');
      setTimeout(() => {
        fetchVideos();
        handleClose();
      }, 1800);
    } catch (err: unknown) {
      setStatus('error');
      setErrorMessage(err instanceof Error ? err.message : 'An error occurred during upload.');
    }
  };

  const handleClose = () => {
    if (status === 'uploading') return;
    setVideoFile(null);
    setThumbnailFile(null);
    if (thumbnailPreview) {
      URL.revokeObjectURL(thumbnailPreview);
      setThumbnailPreview(null);
    }
    setTitle('');
    setDescription('');
    setTranscodingEngine('FFMPEG');
    setEncryptionMethod('None');
    setTargetResourceArn(`az:video:${config.tenantId}:video/00000000-0000-0000-0000-000000000001`);
    setVideoProgress(0);
    setThumbnailProgress(0);
    setStatus('idle');
    setErrorMessage('');
    onClose();
  };

  if (!isOpen) return null;

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm overflow-y-auto">
        <motion.div
          initial={{ opacity: 0, scale: 0.96, y: 16 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.96, y: 16 }}
          transition={{ duration: 0.15, ease: 'easeOut' }}
          className="bg-white rounded-2xl shadow-2xl border border-slate-200/80 w-full max-w-2xl my-8 overflow-hidden"
        >
          {/* Header */}
          <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100">
            <div className="flex items-center gap-2.5">
              <div className="p-2 bg-slate-100 rounded-lg text-slate-800">
                <Film size={18} />
              </div>
              <div>
                <h2 className="text-base font-semibold text-slate-900">Upload Video Asset</h2>
                <p className="text-xs text-slate-500">Ingest to AlphaZero Video Pipeline (S3 &rarr; Step Functions)</p>
              </div>
            </div>
            <button
              onClick={handleClose}
              disabled={status === 'uploading'}
              className="text-slate-400 hover:text-slate-700 hover:bg-slate-100 p-1.5 rounded-lg transition-colors disabled:opacity-40"
            >
              <X size={18} />
            </button>
          </div>

          <div className="p-6">
            {/* Status Views */}
            {status === 'success' && (
              <div className="py-12 text-center space-y-3">
                <div className="w-12 h-12 bg-emerald-100 text-emerald-600 rounded-full flex items-center justify-center mx-auto">
                  <CheckCircle2 size={28} />
                </div>
                <h3 className="text-lg font-semibold text-slate-900">Upload Complete</h3>
                <p className="text-xs text-slate-500 max-w-sm mx-auto">
                  Source files uploaded directly to S3. Step Functions pipeline has been triggered for transcoding & DRM packaging.
                </p>
              </div>
            )}

            {status === 'error' && (
              <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-xl text-red-700 flex items-start gap-3">
                <AlertCircle size={20} className="shrink-0 mt-0.5" />
                <div className="text-xs">
                  <p className="font-semibold">Upload failed</p>
                  <p className="mt-0.5">{errorMessage}</p>
                </div>
              </div>
            )}

            {status === 'uploading' && (
              <div className="py-8 space-y-6">
                <div className="space-y-2">
                  <div className="flex justify-between text-xs font-medium text-slate-700">
                    <span className="flex items-center gap-1.5">
                      <Film size={14} className="text-slate-500" />
                      Uploading Video: {videoFile?.name}
                    </span>
                    <span>{videoProgress}%</span>
                  </div>
                  <div className="h-2 bg-slate-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-slate-900 transition-all duration-150 ease-out"
                      style={{ width: `${videoProgress}%` }}
                    />
                  </div>
                </div>

                {thumbnailFile && (
                  <div className="space-y-2">
                    <div className="flex justify-between text-xs font-medium text-slate-700">
                      <span className="flex items-center gap-1.5">
                        <ImageIcon size={14} className="text-slate-500" />
                        Uploading Thumbnail: {thumbnailFile.name}
                      </span>
                      <span>{thumbnailProgress}%</span>
                    </div>
                    <div className="h-2 bg-slate-100 rounded-full overflow-hidden">
                      <div
                        className="h-full bg-emerald-500 transition-all duration-150 ease-out"
                        style={{ width: `${thumbnailProgress}%` }}
                      />
                    </div>
                  </div>
                )}

                <p className="text-center text-xs text-slate-400">
                  Uploading directly to S3 via presigned PUT URLs with SHA/metadata headers...
                </p>
              </div>
            )}

            {/* Form & Dropzones */}
            {status !== 'success' && status !== 'uploading' && (
              <div className="space-y-5">
                {/* Dual Dropzone Grid */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {/* Video Dropzone */}
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                      Video File <span className="text-red-500">*</span>
                    </label>
                    <div
                      {...getVideoRootProps()}
                      className={clsx(
                        'border-2 border-dashed rounded-xl p-5 text-center cursor-pointer transition-colors duration-150 flex flex-col items-center justify-center min-h-[140px]',
                        isVideoDragActive ? 'border-slate-900 bg-slate-50' : 'border-slate-200 hover:border-slate-400 bg-slate-50/50',
                        videoFile && 'border-emerald-500/70 bg-emerald-50/20'
                      )}
                    >
                      <input {...getVideoInputProps()} />
                      <Upload size={24} className={clsx('mb-2', videoFile ? 'text-emerald-600' : 'text-slate-400')} />
                      {videoFile ? (
                        <div className="text-center">
                          <p className="text-xs font-semibold text-slate-800 truncate max-w-[200px]">{videoFile.name}</p>
                          <p className="text-[10px] text-slate-400 mt-0.5">{(videoFile.size / (1024 * 1024)).toFixed(1)} MB</p>
                        </div>
                      ) : (
                        <div className="text-center">
                          <p className="text-xs font-medium text-slate-700">Drop MP4 video here</p>
                          <p className="text-[10px] text-slate-400 mt-0.5">Click or drag & drop (.mp4)</p>
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Thumbnail Dropzone */}
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                      Custom Thumbnail <span className="text-slate-400 font-normal">(Optional)</span>
                    </label>
                    <div
                      {...getThumbnailRootProps()}
                      className={clsx(
                        'border-2 border-dashed rounded-xl p-5 text-center cursor-pointer transition-colors duration-150 flex flex-col items-center justify-center min-h-[140px] relative overflow-hidden',
                        isThumbnailDragActive ? 'border-slate-900 bg-slate-50' : 'border-slate-200 hover:border-slate-400 bg-slate-50/50',
                        thumbnailFile && 'border-emerald-500/70 bg-emerald-50/20'
                      )}
                    >
                      <input {...getThumbnailInputProps()} />
                      {thumbnailPreview ? (
                        <div className="relative w-full h-full flex flex-col items-center">
                          <img
                            src={thumbnailPreview}
                            alt="Thumbnail Preview"
                            className="h-16 w-28 object-cover rounded-md border border-slate-200 mb-1"
                          />
                          <p className="text-[10px] font-medium text-slate-700 truncate max-w-[180px]">{thumbnailFile?.name}</p>
                        </div>
                      ) : (
                        <>
                          <ImageIcon size={24} className="mb-2 text-slate-400" />
                          <p className="text-xs font-medium text-slate-700">Drop poster image</p>
                          <p className="text-[10px] text-slate-400 mt-0.5">.jpg, .webp, or .png</p>
                        </>
                      )}
                    </div>
                  </div>
                </div>

                {/* Metadata Fields */}
                <div className="space-y-3.5">
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 mb-1">Title</label>
                    <input
                      type="text"
                      value={title}
                      onChange={(e) => setTitle(e.target.value)}
                      placeholder="e.g. Lesson 1: Introduction to Architecture"
                      className="w-full text-xs px-3.5 py-2 rounded-lg border border-slate-200 bg-white focus:outline-none focus:ring-1 focus:ring-slate-900 focus:border-slate-900 transition-all placeholder:text-slate-400"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-700 mb-1">
                      Description <span className="text-slate-400 font-normal">(Optional)</span>
                    </label>
                    <textarea
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                      placeholder="Brief summary of video contents..."
                      rows={2}
                      className="w-full text-xs px-3.5 py-2 rounded-lg border border-slate-200 bg-white focus:outline-none focus:ring-1 focus:ring-slate-900 focus:border-slate-900 transition-all placeholder:text-slate-400 resize-none"
                    />
                  </div>

                  {/* Pipeline Parameters */}
                  <div className="p-3.5 bg-slate-50 border border-slate-200/80 rounded-xl space-y-3">
                    <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-700">
                      <Settings2 size={14} className="text-slate-500" />
                      Pipeline Ingestion Settings
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      <div>
                        <label className="block text-[11px] font-medium text-slate-600 mb-1">Transcoding Engine</label>
                        <select
                          value={transcodingEngine}
                          onChange={(e) => setTranscodingEngine(e.target.value)}
                          className="w-full text-xs px-3 py-1.5 rounded-lg border border-slate-200 bg-white focus:outline-none focus:ring-1 focus:ring-slate-900"
                        >
                          <option value="FFMPEG">FFMPEG (ECS Fargate Task)</option>
                          <option value="MediaConvert">AWS Elemental MediaConvert</option>
                        </select>
                      </div>

                      <div>
                        <label className="block text-[11px] font-medium text-slate-600 mb-1">Encryption / DRM</label>
                        <select
                          value={encryptionMethod}
                          onChange={(e) => setEncryptionMethod(e.target.value)}
                          className="w-full text-xs px-3 py-1.5 rounded-lg border border-slate-200 bg-white focus:outline-none focus:ring-1 focus:ring-slate-900"
                        >
                          <option value="None">None (Standard HLS)</option>
                          <option value="ClearKey">ClearKey (AES-128 cbcs / Shaka)</option>
                        </select>
                      </div>
                    </div>

                    <div>
                      <label className="block text-[11px] font-medium text-slate-600 mb-1">Target Resource ARN</label>
                      <input
                        type="text"
                        value={targetResourceArn}
                        onChange={(e) => setTargetResourceArn(e.target.value)}
                        className="w-full text-xs font-mono px-3 py-1.5 rounded-lg border border-slate-200 bg-white focus:outline-none focus:ring-1 focus:ring-slate-900"
                      />
                    </div>
                  </div>
                </div>

                {/* Footer Buttons */}
                <div className="pt-2 flex justify-end gap-2.5">
                  <button
                    onClick={handleClose}
                    className="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded-lg transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleUpload}
                    disabled={!videoFile || !title.trim()}
                    className="px-5 py-2 text-xs font-semibold bg-slate-900 text-white rounded-lg hover:bg-slate-800 disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                  >
                    Start Ingestion
                  </button>
                </div>
              </div>
            )}
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
};
