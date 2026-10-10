import { useState, useEffect } from 'react';

const CDN_BASE = import.meta.env.VITE_PUBLIC_CDN_BASE || 'https://documents.public.cdn.zadmuslim.cc';

function VariantImage({ variant, cdnUrl, documentId, tenantId, isPublic, apiBase, apiToken, refreshKey }: { variant: string, cdnUrl: string, documentId: string, tenantId: string, isPublic: boolean, apiBase: string, apiToken: string, refreshKey: number }) {
  const [url, setUrl] = useState(isPublic ? cdnUrl : '');
  
  useEffect(() => {
    if (isPublic) {
      setUrl(cdnUrl);
      return;
    }
    
    if (!documentId) return;
    
    const fetchUrl = async () => {
      try {
        const headers: HeadersInit = { 'X-TenantId': tenantId };
        if (apiToken) headers['Authorization'] = `Bearer ${apiToken}`;
        
        const res = await fetch(`${apiBase}/documents/${documentId}/download-url?VariantName=${variant}`, {
          headers
        });
        if (res.ok) {
          const data = await res.json();
          setUrl(data.url);
        }
      } catch (err) {
        console.error('Failed to fetch presigned url', err);
      }
    };
    fetchUrl();
  }, [variant, documentId, tenantId, isPublic, apiBase, apiToken, cdnUrl, refreshKey]);

  const separator = url.includes('?') ? '&' : '?';
  const urlWithCacheBuster = (url && isPublic) ? `${url}${separator}_ts=${refreshKey}` : url;
  
  return (
    <div className="border border-brand-border bg-surface p-4 flex flex-col items-center group transition-all hover:border-accent">
      <h3 className="text-xs font-bold text-accent mb-4 uppercase tracking-widest">{variant.replace('-', ' ')}</h3>
      {urlWithCacheBuster ? (
        <div className="relative w-full flex items-center justify-center h-48 bg-canvas">
          <img 
            src={urlWithCacheBuster} 
            alt={variant} 
            className="max-w-full max-h-48 object-contain transition-opacity duration-300" 
            onError={e => {
              e.currentTarget.style.display = 'none';
              if (e.currentTarget.nextElementSibling) {
                (e.currentTarget.nextElementSibling as HTMLElement).style.display = 'flex';
              }
            }} 
            onLoad={e => {
              e.currentTarget.style.display = 'block';
              if (e.currentTarget.nextElementSibling) {
                (e.currentTarget.nextElementSibling as HTMLElement).style.display = 'none';
              }
            }} 
          />
          <div className="absolute inset-0 flex items-center justify-center text-brand-dark/40 font-mono text-xs hidden">
            PROCESSING
          </div>
        </div>
      ) : (
        <div className="h-48 w-full flex items-center justify-center text-brand-dark/40 bg-canvas font-mono text-xs">AWAITING</div>
      )}
    </div>
  );
}

function MetadataCard({ metadata }: { metadata: any }) {
  return (
    <div className="border border-brand-border bg-surface p-4 text-xs font-mono w-full overflow-x-auto text-brand-dark/80 h-full">
      <pre className="whitespace-pre-wrap">{JSON.stringify(metadata, null, 2)}</pre>
    </div>
  );
}

function App() {
  const [apiBase, setApiBase] = useState(import.meta.env.VITE_API_BASE || 'https://localhost:7016');
  const [apiToken, setApiToken] = useState(import.meta.env.VITE_API_TOKEN || '');
  const [file, setFile] = useState<File | null>(null);
  const [tenantId, setTenantId] = useState('9ac6bf72-f911-452e-a43a-ae9b3e26238c');
  const [profile, setProfile] = useState('Course Cover');
  const [isPublic, setIsPublic] = useState(false);
  const [status, setStatus] = useState('');
  const [uploadProgress, setUploadProgress] = useState(0);
  const [documentId, setDocumentId] = useState('');
  const [documentStatus, setDocumentStatus] = useState('');
  const [documentType, setDocumentType] = useState('');
  const [documentMetadata, setDocumentMetadata] = useState<any>(null);
  const [variantsVisible, setVariantsVisible] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setFile(e.target.files[0]);
    }
  };

  const startPipeline = async () => {
    if (!file) {
      alert("Please select a file first");
      return;
    }

    try {
      setVariantsVisible(false);
      setDocumentId('');
      setDocumentStatus('');
      setDocumentMetadata(null);
      setUploadProgress(0);
      setStatus('Requesting upload URL...');
      
      const uploadReq = {
        title: file.name,
        description: "Uploaded from UIDEMO",
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        fileSizeBytes: file.size,
        profileType: profile,
        IsPublic: isPublic
      };

      const headers: HeadersInit = {
        'Content-Type': 'application/json',
        'X-TenantId': tenantId
      };
      
      if (apiToken) {
        headers['Authorization'] = `Bearer ${apiToken}`;
      }

      const uploadRes = await fetch(`${apiBase}/api/documents/upload`, {
        method: 'POST',
        headers,
        body: JSON.stringify(uploadReq)
      });

      if (!uploadRes.ok) {
        throw new Error(`Failed to get upload URL: ${uploadRes.statusText}`);
      }

      const uploadData = await uploadRes.json();
      const { documentId: newDocId, uploadPresignedUrl, headers: uploadHeaders } = uploadData;
      setDocumentId(newDocId);

      setStatus('Uploading file directly to S3...');
      
      await new Promise<void>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open('PUT', uploadPresignedUrl, true);
        
        if (uploadHeaders && Object.keys(uploadHeaders).length > 0) {
          let hasContentType = false;
          for (const [key, value] of Object.entries(uploadHeaders)) {
            xhr.setRequestHeader(key, value as string);
            if (key.toLowerCase() === 'content-type') hasContentType = true;
          }
          if (!hasContentType) {
            xhr.setRequestHeader('Content-Type', file.type || 'application/octet-stream');
          }
        } else {
          xhr.setRequestHeader('Content-Type', file.type || 'application/octet-stream');
        }

        xhr.upload.onprogress = (e) => {
          if (e.lengthComputable) {
            const percentComplete = (e.loaded / e.total) * 100;
            setUploadProgress(percentComplete);
          }
        };

        xhr.onload = () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            resolve();
          } else {
            reject(new Error(`S3 upload failed with status ${xhr.status}`));
          }
        };

        xhr.onerror = () => reject(new Error('S3 upload failed due to network error'));
        xhr.send(file);
      });

      setStatus('Upload complete. Waiting for processing...');
      setDocumentStatus('Processing');
      setVariantsVisible(true);
      setRefreshKey(Date.now());
      
      setTimeout(() => {
        setStatus('');
        setUploadProgress(0);
      }, 3000);
      
    } catch (err: any) {
      console.error(err);
      setStatus(`Error: ${err.message}`);
      setUploadProgress(0);
    }
  };

  const fetchDocumentDetails = async () => {
    if (!documentId) return;
    try {
      const headers: HeadersInit = {
        'X-TenantId': tenantId
      };
      if (apiToken) {
        headers['Authorization'] = `Bearer ${apiToken}`;
      }
      
      const res = await fetch(`${apiBase}/api/documents/${documentId}`, { headers });
      if (!res.ok) throw new Error('Failed to fetch doc info');
      const data = await res.json();
      
      setDocumentStatus(data.status);
      setDocumentType(data.type);
      if (data.metadata) {
        setDocumentMetadata(data.metadata);
      }
    } catch (e) {
      console.error('Polling error', e);
    }
  };

  // Poll for document status
  useEffect(() => {
    if (documentId && documentStatus === 'Processing') {
      const interval = setInterval(fetchDocumentDetails, 3000);
      return () => clearInterval(interval);
    }
  }, [documentId, documentStatus, apiBase, tenantId, apiToken]);

  const getCdnUrl = (variantPath: string) => {
    if (!variantPath) return '';
    
    // Fix empty GUID placeholder from backend if present
    const fixedPath = variantPath.replace('00000000-0000-0000-0000-000000000000', tenantId);

    // The variant path from backend already has documents/{tenantId}/...
    return `${CDN_BASE}/${fixedPath}`;
  };

  const handleRefresh = () => {
    setRefreshKey(Date.now());
    fetchDocumentDetails();
  };

  return (
    <div className="min-h-screen bg-canvas text-brand-dark py-12 px-4 sm:px-6 lg:px-8 font-sans selection:bg-accent selection:text-white">
      <div className="max-w-5xl mx-auto space-y-12">
        
        <header className="border-b border-brand-border pb-6 flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight text-accent">AlphaZero</h1>
            <p className="text-sm text-brand-dark/60 mt-1 font-mono uppercase tracking-widest">Image Pipeline Console</p>
          </div>
          <div className="text-xs font-mono px-3 py-1 bg-surface border border-brand-border text-brand-dark/70">
            ENV: {import.meta.env.MODE || 'development'}
          </div>
        </header>
        
        <main className="grid grid-cols-1 lg:grid-cols-12 gap-8">
          
          <div className="lg:col-span-5 space-y-8">
            <section className="bg-surface p-6 border border-brand-border">
              <h2 className="text-sm font-bold text-accent uppercase tracking-widest mb-6">Configuration</h2>
              
              <div className="space-y-5">
                <div>
                  <label className="block text-xs font-bold text-brand-dark/70 uppercase tracking-wider mb-2">API Base URL</label>
                  <input type="text" value={apiBase} onChange={e => setApiBase(e.target.value)} className="w-full bg-canvas border border-brand-border p-3 text-sm focus:outline-none focus:border-accent transition-colors font-mono" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-brand-dark/70 uppercase tracking-wider mb-2">API Token</label>
                  <input type="password" value={apiToken} onChange={e => setApiToken(e.target.value)} placeholder="Bearer token..." className="w-full bg-canvas border border-brand-border p-3 text-sm focus:outline-none focus:border-accent transition-colors font-mono" />
                </div>
                <div className="grid grid-cols-1 gap-4">
                  <div>
                    <label className="block text-xs font-bold text-brand-dark/70 uppercase tracking-wider mb-2">Tenant ID</label>
                    <input type="text" value={tenantId} onChange={e => setTenantId(e.target.value)} className="w-full bg-canvas border border-brand-border p-3 text-sm focus:outline-none focus:border-accent transition-colors font-mono" />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4 items-end">
                  <div>
                    <label className="block text-xs font-bold text-brand-dark/70 uppercase tracking-wider mb-2">Profile</label>
                    <select value={profile} onChange={e => setProfile(e.target.value)} className="w-full bg-canvas border border-brand-border p-3 text-sm focus:outline-none focus:border-accent transition-colors appearance-none rounded-none">
                      <option value="Avatar">Avatar</option>
                      <option value="Course Cover">Course Cover</option>
                      <option value="Inline">Inline</option>
                    </select>
                  </div>
                  <div className="pb-3">
                    <label className="flex items-center space-x-3 cursor-pointer group">
                      <div className={`w-5 h-5 border flex items-center justify-center transition-colors ${isPublic ? 'bg-accent border-accent' : 'bg-canvas border-brand-border group-hover:border-accent'}`}>
                        {isPublic && <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="square" strokeLinejoin="miter" strokeWidth="3" d="M5 13l4 4L19 7" /></svg>}
                      </div>
                      <input type="checkbox" checked={isPublic} onChange={e => setIsPublic(e.target.checked)} className="hidden" />
                      <span className="text-xs font-bold uppercase tracking-wider text-brand-dark/80 group-hover:text-accent">Public Asset</span>
                    </label>
                  </div>
                </div>
              </div>
            </section>
            
            <section className="bg-surface p-6 border border-brand-border">
              <h2 className="text-sm font-bold text-accent uppercase tracking-widest mb-6">Upload</h2>
              
              <div className="space-y-6">
                <div>
                  <div className="relative border-2 border-dashed border-brand-border bg-canvas p-8 text-center hover:border-accent transition-colors cursor-pointer group">
                    <input type="file" onChange={handleFileChange} className="absolute inset-0 w-full h-full opacity-0 cursor-pointer" />
                    <div className="text-sm font-mono text-brand-dark/60 group-hover:text-accent transition-colors">
                      {file ? file.name : 'SELECT FILE'}
                    </div>
                  </div>
                </div>
                
                <button 
                  onClick={startPipeline}
                  disabled={!file || status.includes('Requesting') || status.includes('Uploading')}
                  className="w-full py-4 px-6 bg-accent text-white text-sm font-bold uppercase tracking-widest hover:bg-brand-dark transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Execute Pipeline
                </button>
                
                {status && (
                  <div className="border border-brand-border bg-canvas p-4">
                    <div className="flex justify-between items-center mb-2">
                      <div className="text-xs font-mono text-accent">{status}</div>
                      {uploadProgress > 0 && uploadProgress < 100 && (
                        <div className="text-xs font-mono text-brand-dark/60">{Math.round(uploadProgress)}%</div>
                      )}
                    </div>
                    {uploadProgress > 0 && uploadProgress < 100 && (
                      <div className="w-full bg-brand-border h-1">
                        <div className="bg-accent h-1 transition-all duration-300" style={{ width: `${uploadProgress}%` }}></div>
                      </div>
                    )}
                  </div>
                )}
              </div>
            </section>
          </div>
          
          <div className="lg:col-span-7">
            {variantsVisible && documentId ? (
              <section className="bg-surface p-6 border border-brand-border h-full flex flex-col">
                <div className="flex justify-between items-center mb-8">
                  <h2 className="text-sm font-bold text-accent uppercase tracking-widest">
                    Pipeline Status: <span className={documentStatus === 'Ready' ? 'text-brand-success' : 'text-brand-warning'}>{documentStatus || 'Pending'}</span>
                  </h2>
                  <button 
                    onClick={handleRefresh}
                    className="text-xs font-bold uppercase tracking-widest text-brand-dark/60 hover:text-accent transition-colors flex items-center space-x-2"
                  >
                    <span>Refresh</span>
                    <svg className={`w-4 h-4 ${documentStatus === 'Processing' ? 'animate-spin' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="square" strokeLinejoin="miter" strokeWidth="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" /></svg>
                  </button>
                </div>
                
                <div className="flex-grow">
                  {documentStatus === 'Ready' && documentMetadata && documentMetadata.Variants ? (
                    documentType === 'Image' ? (
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                        {Object.entries(documentMetadata.Variants).map(([key, path]) => (
                          <VariantImage key={key} variant={key} cdnUrl={getCdnUrl(path as string)} refreshKey={refreshKey} documentId={documentId} tenantId={tenantId} isPublic={isPublic} apiBase={apiBase} apiToken={apiToken} />
                        ))}
                      </div>
                    ) : (
                      <div className="grid grid-cols-1 gap-6">
                        <MetadataCard metadata={documentMetadata} />
                        <div className="text-xs font-mono text-brand-dark/60 uppercase tracking-widest">
                          {documentType} type not supported for visual preview yet.
                        </div>
                      </div>
                    )
                  ) : (
                    <div className="h-full flex items-center justify-center p-12">
                      <div className="text-center">
                        <div className="text-brand-dark/40 font-mono text-sm mb-4">
                          {documentStatus === 'Processing' ? 'AWAITING EVENTBRIDGE WORKER...' : 'NO OUTPUT VARIANTS YET'}
                        </div>
                        {documentStatus === 'Processing' && (
                          <div className="inline-block w-8 h-8 border-2 border-brand-border border-t-accent rounded-full animate-spin"></div>
                        )}
                      </div>
                    </div>
                  )}
                </div>
                
                <div className="mt-8 p-4 border border-brand-border bg-canvas text-xs font-mono text-brand-dark/60">
                  <span className="text-accent font-bold">SYS_NOTE:</span> Images are delivered via Cloudflare Edge. Variants are fetched directly from document metadata JSONB.
                </div>
              </section>
            ) : (
              <section className="bg-canvas border border-brand-border border-dashed h-full min-h-[400px] flex items-center justify-center p-6 text-center">
                <div className="text-brand-dark/40 font-mono text-sm">
                  NO OUTPUT GENERATED<br/><br/>
                  <span className="text-xs">EXECUTE PIPELINE TO VIEW VARIANTS</span>
                </div>
              </section>
            )}
          </div>
          
        </main>
      </div>
    </div>
  );
}

export default App;
