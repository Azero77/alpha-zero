# Getting Started with AlphaZero Identity

This guide walks you through the process of authenticating a user and obtaining the necessary tokens to interact with AlphaZero Academy features.

## Prerequisites

- Access to an AlphaZero Environment.
- A valid account in the global AWS Cognito User Pool.
- The `TenantId` of the Academy you wish to access.

## The Token Exchange Flow

AlphaZero uses a two-step authentication process to ensure tenant isolation and context awareness.

### 1. Global Authentication
First, the client authenticates against AWS Cognito. This proves the user's identity but grants no permissions within specific Academies.

**Result**: A Global JWT containing the `sub` (IdentityId) claim.

### 2. Tenant Token Exchange
To perform actions within an Academy, you must exchange the Global JWT for a **Tenant-Scoped JWT**.

**Endpoint**: `POST /identity/auth/exchange-tenant-token`

**Request**:
```json
{
  "TenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "DeviceFingerprint": "my-phone-unique-id-v1"
}
```

**What happens on the server**:
1.  Verifies the Global JWT signature.
2.  Locates the `TenantUser` associated with the `IdentityId` and `TenantId`.
3.  Records or validates the `DeviceFingerprint`.
4.  Issues a Scoped JWT signed by AlphaZero.

### 3. Using the Scoped JWT
All subsequent requests to protected Academy endpoints must include the Scoped JWT in the `Authorization` header:

```http
Authorization: Bearer <Scoped_JWT>
```

## Authentication Methods

AlphaZero supports multiple authentication methods depending on the principal type.

| Method | Source | Use Case |
| :--- | :--- | :--- |
| **TenantUser** | Cognito + Exchange | Standard students and academy staff. |
| **Principal** | Local Credentials | Administrative bots, automated scripts, or internal service accounts. |

### Principal Login (Direct)
If you are using an IAM Principal (e.g., a Library Accountant bot), you can login directly without Cognito.

**Endpoint**: `POST /identity/auth/login-principal`

**Request**:
```json
{
  "Username": "accountant_01",
  "Password": "password123",
  "TenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

## Next Steps

- Learn how permissions are scoped using [Resource ARNs](concepts/resource-arns.md).
- Understand how to [Authorize Endpoints](how-to/authorize-endpoints.md) in your own modules.
