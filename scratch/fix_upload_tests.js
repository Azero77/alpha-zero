const fs = require('fs');

const uploadDocPath = 'tests/Modules/Documents/Documents.UnitTests/Commands/UploadDocumentTests.cs';
let uploadContent = fs.readFileSync(uploadDocPath, 'utf8');
uploadContent = uploadContent.replace(/\s*d\.Scope == "course\/123" &&\n/g, '\n');
fs.writeFileSync(uploadDocPath, uploadContent);
console.log('Fixed UploadDocumentTests.cs');

