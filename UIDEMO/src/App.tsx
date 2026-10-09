import { useState } from 'react';

const CDN_BASE = 'https://cdn.alphazero.academy';

function VariantImage({ variant, cdnUrl, refreshKey }: { variant: string, cdnUrl: string, refreshKey: number }) {
  // Append a timestamp or refresh key to bust browser cache
  const urlWithCacheBuster = cdnUrl ? `${cdnUrl}&_ts=${refreshKey}` : '';
  
  return (
    <div className="border rounded-md p-4 flex flex-col items-center">
      <h3 className="text-md font-medium text-gray-700 mb-2 capitalize">{variant.replace('-', ' ')}</h3>
      {urlWithCacheBuster ? (
        <img 
          src={urlWithCacheBuster} 
          alt={variant} 
          className="max-w-full max-h-48 object-contain" 
          onError={e => (e.currentTarget.style.display = 'none')} 
          onLoad={e => (e.currentTarget.style.display = 'block')} 
        />
      ) : (
        <div className="h-48 flex items-center justify-center text-gray-400">Loading...</div>
      )}
    </div>
  );
}

function App() {
  const [apiBase, setApiBase] = useState('https://localhost:7016');
  const [apiToken, setApiToken] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [tenantId, setTenantId] = useState('9ac6bf72-f911-452e-a43a-ae9b3e26238c'); // Default test tenant
  const [scope, setScope] = useState('course/c1');
  const [profile, setProfile] = useState('Course Cover');
  const [isPublic, setIsPublic] = useState(false);
  const [status, setStatus] = useState('');
  const [documentId, setDocumentId] = useState('');
  const [token, setToken] = useState('');
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
      setToken('');
      setStatus('Requesting upload URL...');
      
      const uploadReq = {
        title: file.name,
        description: "Uploaded from UIDEMO",
        scope,
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        fileSizeBytes: file.size,
        profileType: profile,
        isPublic
      };

      const headers: HeadersInit = {
        'Content-Type': 'application/json',
        'x-tenant-id': tenantId
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
      const { documentId: newDocId, uploadPresignedUrl } = uploadData;
      setDocumentId(newDocId);

      setStatus('Uploading file directly to S3...');
      const s3Res = await fetch(uploadPresignedUrl, {
        method: 'PUT',
        headers: {
          'Content-Type': file.type || 'application/octet-stream'
        },
        body: file
      });

      if (!s3Res.ok) {
        throw new Error('Failed to upload file to S3');
      }

      if (!isPublic) {
        setStatus('Requesting CDN access token from Identity module...');
        const tokenHeaders: HeadersInit = {
          'x-tenant-id': tenantId
        };
        
        if (apiToken) {
          tokenHeaders['Authorization'] = `Bearer ${apiToken}`;
        }

        const tokenRes = await fetch(`${apiBase}/api/identity/tokens/document?scope=${encodeURIComponent(scope)}`, {
          method: 'GET',
          headers: tokenHeaders
        });

        if (!tokenRes.ok) {
          throw new Error('Failed to fetch document token');
        }

        const tokenData = await tokenRes.json();
        setToken(tokenData.token);
      } else {
        setToken('public');
      }

      setStatus('Pipeline completed successfully. Wait a few seconds for EventBridge/Step Functions to process variants, then refresh.');
      setVariantsVisible(true);
      setRefreshKey(Date.now());
    } catch (err: any) {
      console.error(err);
      setStatus(`Error: ${err.message}`);
    }
  };

  const getCdnUrl = (variantName: string) => {
    if (!documentId || !token) return '';
    if (isPublic) {
      return `${CDN_BASE}/documents/${tenantId}/images/${documentId}/${variantName}.webp`;
    }
    return `${CDN_BASE}/documents/${tenantId}/${scope}/${documentId}/${variantName}?token=${token}`;
  };

  const handleRefresh = () => {
    setRefreshKey(Date.now());
  };

  return (
    <div className="min-h-screen bg-gray-50 py-10 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto space-y-8">
        
        <div className="bg-white p-6 rounded-lg shadow">
          <h1 className="text-2xl font-bold mb-6 text-gray-900">AlphaZero Image Pipeline Demo</h1>
          
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700">Image File</label>
              <input type="file" accept="image/*" onChange={handleFileChange} className="mt-1 block w-full border border-gray-300 rounded-md p-2" />
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-sm font-medium text-gray-700">API Base URL</label>
                <select value={apiBase} onChange={e => setApiBase(e.target.value)} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm p-2">
                  <option value="https://localhost:7016">https://localhost:7016</option>
                  <option value="http://localhost:5053">http://localhost:5053</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700">API Token (Bearer)</label>
                <input type="text" value={apiToken} onChange={e => setApiToken(e.target.value)} placeholder="e.g. eyJhbGci..." className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm p-2" />
              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <div>
                <label className="block text-sm font-medium text-gray-700">Tenant ID</label>
                <input type="text" value={tenantId} onChange={e => setTenantId(e.target.value)} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm p-2" />
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700">Scope</label>
                <input type="text" value={scope} onChange={e => setScope(e.target.value)} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm p-2" />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700">Profile</label>
                <select value={profile} onChange={e => setProfile(e.target.value)} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm p-2">
                  <option value="Avatar">Avatar</option>
                  <option value="Course Cover">Course Cover</option>
                  <option value="Inline">Inline</option>
                </select>
              </div>

              <div className="flex items-end pb-2">
                <label className="flex items-center space-x-2 cursor-pointer">
                  <input type="checkbox" checked={isPublic} onChange={e => setIsPublic(e.target.checked)} className="h-5 w-5 text-indigo-600 border-gray-300 rounded" />
                  <span className="text-sm font-medium text-gray-700">Is Public</span>
                </label>
              </div>
            </div>

            <button 
              onClick={startPipeline}
              className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700"
            >
              Upload & Process
            </button>

            {status && (
              <div className="mt-4 p-4 rounded-md bg-blue-50 text-blue-700">
                {status}
              </div>
            )}
          </div>
        </div>

        {variantsVisible && documentId && (
          <div className="bg-white p-6 rounded-lg shadow space-y-6">
            <div className="flex justify-between items-center">
              <h2 className="text-xl font-bold text-gray-900">Generated Variants</h2>
              <button 
                onClick={handleRefresh}
                className="py-1 px-3 border border-gray-300 rounded-md text-sm font-medium bg-white hover:bg-gray-50"
              >
                Refresh Images
              </button>
            </div>
            
            <p className="text-sm text-gray-500">
              Note: Images are fetched via the Cloudflare CDN edge router. If you see broken images, ensure the Step Functions pipeline has finished processing.
            </p>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              <VariantImage variant="original" cdnUrl={getCdnUrl('original')} refreshKey={refreshKey} />
              <VariantImage variant="thumbnail" cdnUrl={getCdnUrl('thumbnail')} refreshKey={refreshKey} />
              <VariantImage variant="web-optimized" cdnUrl={getCdnUrl('web-optimized')} refreshKey={refreshKey} />
            </div>
          </div>
        )}

      </div>
    </div>
  );
}

export default App;
