# 🪐 AlphaZero Library Module: Physical Economy Integration Test Scenarios

This document outlines the exhaustive integration test suite for the Library Module. These tests verify the lifecycle of physical access codes, library management, and cross-module resource authorization.

## 🏛️ 1. Library Management & Multi-Tenancy

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Create Library** | Verify basic persistence. | Library saved; `TenantId` correctly assigned. |
| **Cross-Tenant Isolation** | Security check. | Tenant A cannot view or modify Tenant B's libraries. |
| **Authorize Resource** | Link course to library. | Resource Pattern added to `LibraryAllowedResources` join table. |
| **Deauthorize Resource** | Remove link. | Pattern removed; Library can no longer mint codes for that course. |

## 🏛️ 2. Access Code Generation (Minting)

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Generate Batch (Valid)** | Verify bulk generation. | N codes created in DB; Status is `Minted`; BatchId assigned. |
| **Generate for Unauthorized Resource** | Security check. | Minting fails with `Forbidden` if Library isn't linked to the ARN. |
| **Generate Admin Code** | direct grant. | Single code created with `ADM-` prefix and no LibraryId. |
| **Password Hashing** | Security check. | Raw codes are NOT stored in DB; only hashes are present. |

## 🏛️ 3. Lifecycle Transitions

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Distribute Batch** | Verify bulk update. | All codes in a batch transition from `Minted` to `Distributed`. |
| **Void Unredeemed Code** | Revocation check. | Status transitions to `Voided`; redemption becomes impossible. |
| **Void Redeemed Code** | Revocation check. | Status transitions to `Voided`; Integration event triggered to revoke access. |

## 🏛️ 4. Redemption Workflow

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Redeem Valid Code** | E2E Check. | Code status `Redeemed`; `RedeemedByUserId` set; Strategy executed. |
| **Redeem Mismatched Tenant** | Security check. | Code for Academy A cannot be used by a user in Academy B. |
| **Redeem Voided/Used Code** | Fraud check. | Error `Conflict` returned; double-spending prevented. |

## 🏛️ 5. Strategy Execution (Cross-Module)

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Course Enrollment Strategy** | Module decoupling. | Redeeming a course code correctly triggers enrollment logic in Courses. |
| **Revocation Strategy** | Module decoupling. | Voiding a code triggers revocation integration event. |