import React, { useState } from 'react';
import { Upload, FileVideo, CheckCircle, AlertCircle, Loader2 } from 'lucide-react';
import { getPresignedUrl } from '../../services/api';
import { uploadToS3 } from '../../services/s3';
import './styles.css';

const UploadDemo: React.FC = () => {
  const [file, setFile] = useState<File | null>(null);
  const [status, setStatus] = useState<'idle' | 'uploading' | 'success' | 'error'>('idle');
  const [progress, setProgress] = useState(0);
  const [errorMessage, setErrorMessage] = useState('');
  const [uploadedKey, setUploadedKey] = useState('');

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setFile(e.target.files[0]);
      setStatus('idle');
      setProgress(0);
      setUploadedKey('');
    }
  };

  const handleUpload = async () => {
    if (!file) return;

    setStatus('uploading');
    setProgress(0);
    setErrorMessage('');

    try {
      // Step 1: Request Pre-signed URL from API
      const { key, preSignedUrl } = await getPresignedUrl(file.name, file.type);
      
      // Step 2: Upload directly to S3
      await uploadToS3(preSignedUrl, file, (p) => setProgress(p));

      setStatus('success');
      setUploadedKey(key);
    } catch (error: any) {
      console.error('Upload failed:', error);
      setStatus('error');
      setErrorMessage(error.response?.data?.message || error.message || 'Unknown error occurred');
    }
  };

  return (
    <div className="upload-container">
      <h2 className="upload-title">S3 Upload Demo</h2>
      
      <div className="form-content">
        {/* File Input */}
        <div className="form-group">
          <label className="label">Select Video File</label>
          <div className="file-input-wrapper">
            <label className="custom-file-upload">
              <FileVideo size={20} style={{ marginRight: '8px' }} />
              <span>{file ? 'Change File' : 'Choose File'}</span>
              <input type="file" className="hidden" style={{ display: 'none' }} onChange={handleFileChange} accept="video/*" />
            </label>
            {file && <span className="file-name">{file.name}</span>}
          </div>
        </div>

        {/* Upload Button */}
        <button
          onClick={handleUpload}
          disabled={!file || status === 'uploading'}
          className="upload-button"
        >
          {status === 'uploading' ? (
            <>
              <Loader2 size={20} className="animate-spin" />
              Uploading... {progress}%
            </>
          ) : (
            <>
              <Upload size={20} />
              Start Upload to S3
            </>
          )}
        </button>

        {/* Progress Bar */}
        {status === 'uploading' && (
          <div className="progress-bar-container">
            <div 
              className="progress-bar-fill" 
              style={{ width: `${progress}%` }}
            ></div>
          </div>
        )}

        {/* Status Indicators */}
        {status === 'success' && (
          <div className="status-box status-success">
            <CheckCircle size={20} style={{ flexShrink: 0 }} />
            <div>
              <p className="status-title">Upload Successful!</p>
              <p className="status-detail text-sm">Key: <span className="font-mono">{uploadedKey}</span></p>
              <p className="status-detail" style={{ fontSize: '11px', marginTop: '4px', fontStyle: 'italic' }}>
                SQS event should trigger MassTransit soon.
              </p>
            </div>
          </div>
        )}

        {status === 'error' && (
          <div className="status-box status-error">
            <AlertCircle size={20} style={{ flexShrink: 0 }} />
            <div>
              <p className="status-title">Upload Failed</p>
              <p className="status-detail">{errorMessage}</p>
            </div>
          </div>
        )}
      </div>

      <div className="footer-info">
        <p>API: <span className="font-mono">{import.meta.env.VITE_API_BASE_URL}</span></p>
        <p style={{ marginTop: '4px' }}>Flow: UI &rarr; API (Sign) &rarr; S3 (PUT) &rarr; SQS &rarr; MassTransit</p>
      </div>
    </div>
  );
};

export default UploadDemo;
