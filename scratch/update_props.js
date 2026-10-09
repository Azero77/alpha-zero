const fs = require('fs');

const path = 'Directory.Packages.props';
let content = fs.readFileSync(path, 'utf8');

// The code specifies version 2.1.13 which is probably vulnerable. Let's try 3.1.5 for ImageSharp.
// If it breaks, we'll try 2.1.9.
content = content.replace(/<PackageVersion Include="SixLabors.ImageSharp" Version="2.1.13" \/>/, '<PackageVersion Include="SixLabors.ImageSharp" Version="3.1.5" />');

// Let's try 2024.3.0 or similar for SSH.NET? Wait, I can try removing it if it's transitive and let pinning handle it? 
// Or I can update it. Let me try 2024.2.0 -> 2024.1.0 maybe? No, later version is 2024.3.0. Or 2023.0.1.
// Let's just update it to something newer or older depending on what's available.
// Wait, I can use `dotnet add package SSH.NET` to get the latest.
fs.writeFileSync(path, content);
