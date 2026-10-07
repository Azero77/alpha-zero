import re

with open('UIDEMO/src/App.tsx', 'r') as f:
    content = f.read()

# Add isPublic state
content = content.replace(
    "const [profile, setProfile] = useState('Course Cover');",
    "const [profile, setProfile] = useState('Course Cover');\n  const [isPublic, setIsPublic] = useState(false);"
)

# Add checkbox
checkbox_html = """              <div>
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
              </div>"""
content = content.replace(
    """              <div>
                <label className="block text-sm font-medium text-gray-700">Profile</label>
                <select value={profile} onChange={e => setProfile(e.target.value)} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm p-2">
                  <option value="Avatar">Avatar</option>
                  <option value="Course Cover">Course Cover</option>
                  <option value="Inline">Inline</option>
                </select>
              </div>""",
    checkbox_html
)

# Update upload request payload
content = content.replace(
    "profileType: profile",
    "profileType: profile,\n        isPublic"
)

# Token fetching logic
token_logic = """      if (!isPublic) {
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
      } else {
        setToken('public');
      }"""
content = content.replace(
    """      setStatus('Requesting CDN access token from Identity module...');
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
      setToken(tokenData.token);""",
    token_logic
)

# getCdnUrl logic
cdn_url_logic = """  const getCdnUrl = (variantName: string) => {
    if (!documentId || !token) return '';
    if (isPublic) {
      return `${CDN_BASE}/documents/${tenantId}/images/${documentId}/${variantName}.webp`;
    }
    return `${CDN_BASE}/documents/${tenantId}/${scope}/${documentId}/${variantName}?token=${token}`;
  };"""
content = content.replace(
    """  const getCdnUrl = (variantName: string) => {
    if (!documentId || !token) return '';
    return `${CDN_BASE}/documents/${tenantId}/${scope}/${documentId}/${variantName}?token=${token}`;
  };""",
    cdn_url_logic
)

with open('UIDEMO/src/App.tsx', 'w') as f:
    f.write(content)
