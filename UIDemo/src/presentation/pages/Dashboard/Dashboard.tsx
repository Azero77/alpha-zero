import React, { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Plus, 
  Play, 
  Trash2, 
  Upload as UploadIcon, 
  RefreshCw, 
  Clock, 
  CheckCircle2, 
  AlertCircle,
  X
} from 'lucide-react';
import { videoService } from '../../../infrastructure/services/VideoService';
import { VideoStatus, type Video, type StreamingInfo } from '../../../domain/models/video';
import VideoPlayer from '../../../components/VideoPlayer/VideoPlayer';
import styles from './Dashboard.module.css';
import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

const Dashboard: React.FC = () => {
  const [videos, setVideos] = useState<Video[]>([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [selectedVideo, setSelectedVideo] = useState<Video | null>(null);
  const [streamingInfo, setStreamingInfo] = useState<StreamingInfo | null>(null);
  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);

  useEffect(() => {
    fetchVideos();
    const interval = setInterval(fetchVideos, 10000); // Poll for updates
    return () => clearInterval(interval);
  }, []);

  const fetchVideos = async () => {
    try {
      const result = await videoService.getVideos(1, 20);
      setVideos(result.items);
    } catch (error) {
      console.error('Failed to fetch videos', error);
    } finally {
      setLoading(false);
    }
  };

  const handleVideoSelect = async (video: Video) => {
    console.log('Video selected:', video.id, 'Status:', video.status);
    
    // Status 1 is Published in C# domain enum
    if (video.status !== VideoStatus.Published) {
      console.warn('Video is not published, status:', video.status);
      return;
    }
    
    setSelectedVideo(video);
    setStreamingInfo(null);
    
    try {
      console.log('Fetching streaming info for:', video.id);
      const info = await videoService.getStreamingInfo(video.id);
      console.log('Streaming info received:', info);
      setStreamingInfo(info);
    } catch (error) {
      console.error('Failed to fetch streaming info', error);
      alert('Failed to fetch streaming info from server.');
    }
  };

  const getStatusLabel = (status: VideoStatus) => {
    switch (status) {
      case VideoStatus.Processing: return 'Processing';
      case VideoStatus.Published: return 'Published';
      case VideoStatus.Failed: return 'Failed';
      case VideoStatus.Deleted: return 'Deleted';
      default: return 'Unknown';
    }
  };

  const getStatusIcon = (status: VideoStatus) => {
    switch (status) {
      case VideoStatus.Published: return <CheckCircle2 className={styles.statusSuccess} size={16} />;
      case VideoStatus.Processing: return <RefreshCw className={cn(styles.statusProcessing, styles.spin)} size={16} />;
      case VideoStatus.Failed: return <AlertCircle className={styles.statusError} size={16} />;
      default: return <Clock className={styles.statusPending} size={16} />;
    }
  };

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    setUploadProgress(0);

    try {
      const uploadRequest = {
        fileName: file.name,
        contentType: file.type,
      };

      const response = await videoService.requestUpload(uploadRequest);
      
      await videoService.uploadFile(
        response.preSignedUrl, 
        file, 
        {
          fileName: file.name,
          videoId: response.videoId,
          tenantId: response.tenantId,
        },
        (percent) => setUploadProgress(percent)
      );

      setIsUploadModalOpen(false);
      fetchVideos();
    } catch (error) {
      console.error('Upload failed', error);
      alert('Upload failed. Check console for details.');
    } finally {
      setUploading(false);
      setUploadProgress(0);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this video?')) return;
    try {
      await videoService.deleteVideo(id);
      fetchVideos();
      if (selectedVideo?.id === id) setSelectedVideo(null);
    } catch (error) {
      console.error('Delete failed', error);
    }
  };

  return (
    <div className={styles.dashboard}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Video Management</h1>
          <p className={styles.subtitle}>Upload, process, and stream your educational content.</p>
        </div>
        <button 
          className={styles.uploadButton}
          onClick={() => setIsUploadModalOpen(true)}
        >
          <Plus size={20} />
          <span>New Video</span>
        </button>
      </header>

      <main className={styles.content}>
        <section className={styles.videoSection}>
          {selectedVideo && streamingInfo ? (
            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              className={styles.playerContainer}
            >
              <div className={styles.playerHeader}>
                <h3>{selectedVideo.title}</h3>
                <button onClick={() => { setSelectedVideo(null); setStreamingInfo(null); }} className={styles.closeButton}>
                  <X size={20} />
                </button>
              </div>
              <VideoPlayer 
                manifestUrl={streamingInfo.manifestUrl} 
                clearKey={streamingInfo.keyId && streamingInfo.key ? { keyId: streamingInfo.keyId, key: streamingInfo.key } : undefined}
              />
              <div className={styles.playerMeta}>
                <p>{selectedVideo.description || 'No description available.'}</p>
                <div className={styles.specs}>
                  <span>{selectedVideo.specifications?.duration}</span>
                  <span>{selectedVideo.specifications?.resolution.width}x{selectedVideo.specifications?.resolution.height}</span>
                </div>
              </div>
            </motion.div>
          ) : selectedVideo ? (
             <div className={styles.emptyPlayer}>
              <RefreshCw size={48} className={styles.spin} strokeWidth={1} />
              <p>Fetching streaming information...</p>
            </div>
          ) : (
            <div className={styles.emptyPlayer}>
              <Play size={48} strokeWidth={1} />
              <p>Select a video to start playing</p>
            </div>
          )}
        </section>

        <section className={styles.listSection}>
          <div className={styles.listHeader}>
            <h3>Your Library</h3>
            <button onClick={fetchVideos} className={styles.refreshButton}>
              <RefreshCw size={16} className={loading ? styles.spin : ''} />
            </button>
          </div>

          <div className={styles.grid}>
            {loading && videos.length === 0 ? (
              <p>Loading videos...</p>
            ) : videos.length === 0 ? (
              <div className={styles.emptyState}>
                <UploadIcon size={32} />
                <p>No videos found. Upload your first one!</p>
              </div>
            ) : (
              videos.map((video) => (
                <motion.div 
                  key={video.id} 
                  layout
                  className={cn(styles.card, selectedVideo?.id === video.id && styles.activeCard)}
                  onClick={() => handleVideoSelect(video)}
                >
                  <div className={styles.cardHeader}>
                    <div className={styles.statusBadge}>
                      {getStatusIcon(video.status)}
                      <span>{getStatusLabel(video.status)}</span>
                    </div>
                    <button 
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDelete(video.id);
                      }} 
                      className={styles.deleteButton}
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                  <h4 className={styles.videoTitle}>{video.title}</h4>
                  <p className={styles.videoDate}>
                    {new Date(video.createdOn).toLocaleDateString()}
                  </p>
                </motion.div>
              ))
            )}
          </div>
        </section>
      </main>

      <AnimatePresence>
        {isUploadModalOpen && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className={styles.modalOverlay}
          >
            <motion.div 
              initial={{ scale: 0.9, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.9, opacity: 0 }}
              className={styles.modal}
            >
              <div className={styles.modalHeader}>
                <h2>Upload New Video</h2>
                <button onClick={() => !uploading && setIsUploadModalOpen(false)}>
                  <X size={24} />
                </button>
              </div>
              
              <div className={styles.uploadArea}>
                {uploading ? (
                  <div className={styles.progressContainer}>
                    <div className={styles.progressBar}>
                      <motion.div 
                        className={styles.progressFill}
                        initial={{ width: 0 }}
                        animate={{ width: `${uploadProgress}%` }}
                      />
                    </div>
                    <p>Uploading... {uploadProgress}%</p>
                  </div>
                ) : (
                  <label className={styles.dropZone}>
                    <UploadIcon size={48} />
                    <p>Click to select an MP4 file</p>
                    <span>Maximum file size: 500MB</span>
                    <input 
                      type="file" 
                      accept="video/mp4" 
                      onChange={handleUpload} 
                      className="sr-only" 
                    />
                  </label>
                )}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
};

export default Dashboard;
