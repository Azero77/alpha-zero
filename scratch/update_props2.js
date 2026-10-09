const fs = require('fs');

const path = 'Directory.Packages.props';
let content = fs.readFileSync(path, 'utf8');

// Update SSH.NET
content = content.replace(/<PackageVersion Include="SSH.NET" Version=".*?" \/>/g, '<PackageVersion Include="SSH.NET" Version="2026.0.0" />');
// Update SixLabors.ImageSharp
content = content.replace(/<PackageVersion Include="SixLabors.ImageSharp" Version=".*?" \/>/g, '<PackageVersion Include="SixLabors.ImageSharp" Version="3.1.12" />');

fs.writeFileSync(path, content);
