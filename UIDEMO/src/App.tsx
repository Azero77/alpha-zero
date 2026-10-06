import { useState } from 'react';

const API_BASE = 'http://localhost:5000'; // Adjust as needed for local API
const CDN_BASE = 'https://cdn.alphazero.academy';

function App() {
  const [file, setFile] = useState<File | null>(null);
  const [tenantId, setTenantId] = useState('00000000-0000-0000-0000-000000000001'); // Default test tenant
  const [scope, setScope] = useState('course/c1');
  const [profile, setProfile] = useState('Course Cover');
  const [status, setStatus] = useState('');
  const [documentId, setDocumentId] = useState('');
  const [token, setToken] = useState('');
  const [variantsVisible, setVariantsVisible] = useState(false);

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
        profileType: profile
      };

      const uploadRes = await fetch(`${API_BASE}/api/documents/upload`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-tenant-id': tenantId
        },
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

      setStatus('Requesting CDN access token from Identity module...');
      const tokenRes = await fetch(`${API_BASE}/api/identity/tokens/document?scope=${encodeURIComponent(scope)}`, {
        method: 'GET',
        headers: {
          'x-tenant-id': tenantId
        }
      });

      if (!tokenRes.ok) {
        throw new Error('Failed to fetch document token');
      }

      const tokenData = await tokenRes.json();
      setToken(tokenData.token);

      setStatus('Pipeline completed successfully. Wait a few seconds for EventBridge/Step Functions to process variants, then refresh.');
      setVariantsVisible(true);
    } catch (err: any) {
      console.error(err);
      setStatus(`Error: ${err.message}`);
    }
  };

  const getCdnUrl = (variantName: string) => {
    if (!documentId || !token) return '';
    return `${CDN_BASE}/documents/${tenantId}/${scope}/${documentId}/${variantName}?token=${token}`;
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
                onClick={() => setVariantsVisible(v => !v)} // Hack to trigger re-render
                className="py-1 px-3 border border-gray-300 rounded-md text-sm font-medium bg-white hover:bg-gray-50"
              >
                Refresh Images
              </button>
            </div>
            
            <p className="text-sm text-gray-500">
              Note: Images are fetched via the Cloudflare CDN edge router. If you see broken images, ensure the Step Functions pipeline has finished processing.
            </p>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              <div className="border rounded-md p-4 flex flex-col items-center">
                <h3 className="text-md font-medium text-gray-700 mb-2">Original</h3>
                <img src={getCdnUrl('original')} alt="Original" className="max-w-full max-h-48 object-contain" onError={e => (e.currentTarget.style.display = 'none')} onLoad={e => (e.currentTarget.style.display = 'block')} />
              </div>
              
              <div className="border rounded-md p-4 flex flex-col items-center">
                <h3 className="text-md font-medium text-gray-700 mb-2">Thumbnail</h3>
                <img src={getCdnUrl('thumbnail')} alt="Thumbnail" className="max-w-full max-h-48 object-contain" onError={e => (e.currentTarget.style.display = 'none')} onLoad={e => (e.currentTarget.style.display = 'block')} />
              </div>
              
              <div className="border rounded-md p-4 flex flex-col items-center">
                <h3 className="text-md font-medium text-gray-700 mb-2">Web-Optimized</h3>
                <img src={getCdnUrl('web-optimized')} alt="Web Optimized" className="max-w-full max-h-48 object-contain" onError={e => (e.currentTarget.style.display = 'none')} onLoad={e => (e.currentTarget.style.display = 'block')} />
              </div>
            </div>
          </div>
        )}

      </div>
    </div>
  );
}

export default App;
