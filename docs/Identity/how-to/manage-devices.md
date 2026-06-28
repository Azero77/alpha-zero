# How to Manage Devices

AlphaZero includes a **Context-Aware Device Enforcement** system to prevent account sharing and ensure that high-value resources (like videos) are only accessed from authorized devices.

## Device Identification via Public Keys

Instead of easily spoofable client-generated strings, we use cryptographic signatures to verify the requesting device.

### How it's captured
1.  **Token Exchange**: During the `exchange-tenant-token` login flow, the frontend sends a `publicKey`, `deviceName`, and `platform`.
2.  **Auto-Registration**: The Identity module records this device in the `UserDevices` table if it hasn't been seen before.
3.  **Secure Requests**: The frontend uses its secure hardware-backed private key to sign the request path and timestamp, sending them as `X-Signature` and `X-Device-Id` headers.

---

## Device Locking (The "Main Device")

To prevent multiple users from sharing a single account to watch videos, an Academy can enforce a "Main Device" lock.

### 1. Registering a Device
Users can have multiple registered devices. **This happens automatically** when they log in to the tenant using `POST /identity/auth/exchange-tenant-token` by providing their public key.

### 2. Setting the Main Device
- **Automatic**: The very first device a user logs in with automatically becomes their "Main Device".
- **Manual Change**: A user can designate a different device as their "Main Device". This is subject to a cooldown period.
**Endpoint**: `POST /identity/users/devices/set-main`

### 3. Enforcement in Policies
The `MainDeviceOnly` condition can be added to any policy statement.

```json
{
  "Effect": true,
  "Actions": ["video:Stream"],
  "Resources": ["az:video:T1:*"],
  "Condition": { "Type": "MainDeviceOnly" }
}
```

---

## Technical Flow of Enforcement

When the `PolicyEvaluatorService` encounters a `MainDeviceOnly` condition:

1.  **Extract Headers**: It gets the `X-Device-Id`, `X-Timestamp`, and `X-Signature` from the request.
2.  **Verify Signature**: It looks up the public key for the provided `DeviceId` and verifies the signature of the payload (`{RequestPath}:{Timestamp}`).
3.  **Check Main Device**: It ensures the `DeviceId` matches the `TenantUser`'s `MainDeviceId`.
4.  **Compare**:
    - **Match & Valid Signature**: Access is allowed.
    - **Mismatch or Invalid Signature**: Access is denied.

## User Experience Tips

1.  **Inform the User**: When access is denied due to a device mismatch, clearly explain that they must use their primary device or update their settings.
2.  **Limit Changes**: Enforce a cooldown period (e.g., 30 days) between main device changes to prevent abuse.
3.  **Graceful Degradation**: Consider allowing low-value actions (like browsing the catalog) from any device, while restricting high-bandwidth actions (like video streaming) to the main device.
