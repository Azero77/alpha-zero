const fs = require('fs');

const path = 'src/alphazero-api/Modules/Identity/Domain/Models/TenantPrincipalAssignment.cs';
let content = fs.readFileSync(path, 'utf8');
content = content.replace(/public required TenantUser TenantUser { get; private set; }/g, 'public TenantUser TenantUser { get; private set; } = null!;');
fs.writeFileSync(path, content);
console.log('Fixed TenantPrincipalAssignment.cs');

