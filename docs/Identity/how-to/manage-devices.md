# How to Manage Devices

AlphaZero includes a **Context-Aware Device Enforcement** system to prevent account sharing and ensure that high-value resources (like videos) are only accessed from authorized devices.

## The Device Fingerprint

Every request that requires tenant-scoped access must include a `DeviceFingerprint`. This is a client-generated unique identifier for the device.

### How it's captured
1.  **Token Exchange**: During the `exchange-tenant-token` flow, the fingerprint is sent to the server.
2.  **Storage**: The IAM module records the fingerprint in the `TenantUser` record and the `UserDevices` table.
3.  **JWT Inclusion**: The fingerprint is embedded in the Scoped JWT as a claim.

---

## Device Locking (The "Main Device")

To prevent multiple users from sharing a single account to watch videos, an Academy can enforce a "Main Device" lock.

### 1. Registering a Device
Users can have multiple registered devices.
**Endpoint**: `POST /identity/users/devices/register`

### 2. Setting the Main Device
A user designates one device as their "Main Device". This is often limited to a certain number of changes per month.
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

1.  **Extract Fingerprint**: It gets the `DeviceFingerprint` from the current Scoped JWT.
2.  **Lookup Main Device**: It queries the `Identity` module for the `TenantUser`'s `ActiveDeviceFingerprint`.
3.  **Compare**:
    - **Match**: Access is allowed.
    - **Mismatch**: Access is denied with a `Device.Mismatch` error.

## User Experience Tips

1.  **Inform the User**: When access is denied due to a device mismatch, clearly explain that they must use their primary device or update their settings.
2.  **Limit Changes**: Enforce a cooldown period (e.g., 30 days) between main device changes to prevent abuse.
3.  **Graceful Degradation**: Consider allowing low-value actions (like browsing the catalog) from any device, while restricting high-bandwidth actions (like video streaming) to the main device.
