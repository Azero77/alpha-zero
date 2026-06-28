# How to Implement Secure Device Flow (Frontend)

To access content locked by the `IsMainDevice` condition, the frontend must participate in the signature verification flow.

## 1. Registering the Device
When a user logs in on a new device, you must register it simultaneously with the token exchange.

1. **Generate a Keypair**: Generate an RSA-256 keypair on the device.
2. **Secure the Private Key**: Store the private key in the platform's secure storage (KeyStore on Android, Keychain on iOS, or an encrypted IndexedDB/WebCrypto for Web).
3. **Send the Public Key**: Call the login/token exchange endpoint with the public key in PEM format. The API will auto-register the device and return the new `deviceId` alongside your token.

```typescript
// Example Login/Exchange Request
const response = await api.post('/identity/auth/exchange-tenant-token', {
  tenantId: "your-tenant-uuid",
  deviceName: "iPhone 15 Pro",
  platform: "Ios",
  publicKey: "-----BEGIN PUBLIC KEY-----\n..."
});

const { token, tenantUserId, deviceId } = response.data;
// Store deviceId for later use in signing requests
```

## 2. Signing Requests
For every request to a secured endpoint (e.g., video streaming), the frontend must include signature headers.

### The Algorithm
1. **Timestamp**: Get current Unix timestamp in seconds.
2. **Path**: The relative path of the resource (e.g., `/video/math-lesson-1`).
3. **Data to Sign**: String concat: `path:timestamp`.
4. **Sign**: Use the **Private Key** to sign the data using `RSA-SHA256`.

### The Headers
Attach these headers to your HTTP request:

| Header | Value |
| :--- | :--- |
| `X-Device-Id` | The UUID of the device received during registration. |
| `X-Timestamp` | The Unix timestamp used in the signature. |
| `X-Signature` | The Base64 encoded signature. |

### Example (TypeScript/WebCrypto)
```typescript
const timestamp = Math.floor(Date.now() / 1000).toString();
const path = "/video/streaming/lesson-45";
const data = `${path}:${timestamp}`;

const signature = await signDataWithPrivateKey(privateKey, data);

const headers = {
  'X-Device-Id': deviceId,
  'X-Timestamp': timestamp,
  'X-Signature': btoa(signature)
};

const video = await fetch(`https://api.az.com${path}`, { headers });
```

## 3. Handling Verification Failures
If the signature is invalid or the timestamp has expired (5-minute window), the server will return `403 Forbidden`.

Common causes:
- **Clock Drift**: Ensure the device time is synchronized with a network time server.
- **Wrong Path**: The path in the signature must exactly match the request path on the server.
- **Expired Keys**: If the user has switched their "Main Device", old devices will fail verification even with valid signatures.
