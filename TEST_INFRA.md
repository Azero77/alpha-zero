# E2E Test Infra: AlphaZero Frontend

## Test Philosophy
- Opaque-box, requirement-driven. Tests verify actual UI rendering, state preservation, and DOM updates against the local backend, without internal mocking.
- Methodology: Category-Partition + Boundary Value Analysis + Pairwise Combination + Real-World Workloads.

## Feature Inventory
| # | Feature | Source (requirement) | Tier 1 | Tier 2 | Tier 3 |
|---|---------|---------------------|:------:|:------:|:------:|
| 1 | Tenant Subdomain Rewrite | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| 2 | Auth Exchange Flow | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| 3 | Student Dashboard | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| 4 | Playback & Progress Sync | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| 5 | Library Code Redemption | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| 6 | Device Lockdown Flow | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| 7 | Teacher Code Management | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |

## Test Architecture
- **Test Runner**: Playwright (`npx playwright test`)
- **Base URL Configuration**: dynamic domain mapping (e.g., `http://localhost:3000` / `http://damascus.localhost:3000` / `http://aleppo.localhost:3000`)
- **Directory Layout**: `frontend-web/tests/`

## Coverage Thresholds
- **Tier 1: Feature Coverage (35 cases)**: Happy-path flows verifying all features in isolation.
- **Tier 2: Boundary & Corner Cases (35 cases)**: Testing empty dashboards, incorrect codes, device fingerprint changes, playback edge-cases.
- **Tier 3: Cross-Feature Combinations (7 cases)**: Redeem code -> unlock course -> stream video -> complete lesson progress update -> dashboard percentage updates.
- **Tier 4: Real-World Workload Scenarios (5 cases)**: Complete student onboarding flow, multi-tenant learning journey, teacher batch code provisioning and student redemption lifecycle.
- **Total Minimum Cases**: 82 test cases.
