## Golden Rule
- Each resource in the app belongs to a tenant.
- Each tenant has a unique identifier (tenantId) that is used to associate resources with the tenant.
- Tenant could be a company, organization, teacher,student,or any logical grouping of users.
- Resources can be anything that the app manages, such as documents, videos, exams
- Resources are created, accessed, and managed within the context of a tenant, ensuring that data is isolated and secure for each tenant.
- The app enforces access control based on tenantId, ensuring that users can only access resources that belong to their tenant.
- This approach allows for multi-tenancy, where multiple tenants can use the same application while keeping their data separate and secure.

## How to handle users logged in many tenants?
- When a user is logged into multiple tenants, the app should provide a way for the user to switch between tenants. This can be done through a tenant selection interface, where the user can choose which tenant they want to access.
- for each tenant , the user will have a separate session, and the app should ensure that the user can only access resources that belong to the currently selected tenant. (jwt per tenants)
- The app should also provide a way for the user to log out of a tenant, which will end the session for that tenant and prevent access to its resources.

## How to handle resources that belong to multiple tenants?
- Resources can't be shared across tenants unless they are global, if it is a premium content it can't be shared across tenants, if it is a free content it can be shared across tenants but it should be marked as global and it should be accessible to all tenants.
- and the resource or its tenant doesn't know who shares them , it is only marked as global and accessible to all tenants, but it is not associated with any specific tenant. This allows for resources to be shared across tenants without compromising the isolation and security of each tenant's data.

## different Users with Different Tenants Authorization

- you can View Authentication and Authorization Section to have the full idea