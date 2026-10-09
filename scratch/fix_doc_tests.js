const fs = require('fs');

const docTestsPath = 'tests/Modules/Documents/Documents.UnitTests/DocumentTests.cs';
let content = fs.readFileSync(docTestsPath, 'utf8');

// Remove the test for scope empty
content = content.replace(/    \[Theory\]\n    \[InlineData\(""\)\]\n    \[InlineData\(" "\)\]\n    \[InlineData\(null\)\]\n    public void Create_Should_Fail_WhenScopeIsEmpty\(string\? scope\)\n    \{\n[\s\S]*?    \}\n\n/g, '');

// Replace `_clock)` with `_clock.Now)` in Create calls
content = content.replace(/, _clock\)/g, ', _clock.Now)');

// Replace the 5th string argument `"course/c1",\n            ` or similar with nothing, BUT let's be more robust:
// The arguments were:
// id, tenantId, title, description, scope, fileType, s3key, size, profileType, isPublic, clock.Now
// We need to remove the 5th argument.

content = content.replace(/(\n\s*id,\n\s*tenantId,\n\s*"Course Syllabus",\n\s*"Full syllabus for 2026",\n)\s*"course\/c1",\n/g, '$1');
content = content.replace(/(\n\s*id,\n\s*tenantId,\n\s*title!,\n\s*null,\n)\s*"course\/c1",\n/g, '$1');
content = content.replace(/(\n\s*id,\n\s*tenantId,\n\s*"Valid Title",\n\s*null,\n)\s*"course\/c1",\n/g, '$1');
content = content.replace(/(\n\s*Guid\.NewGuid\(\),\n\s*Guid\.NewGuid\(\),\n\s*"Title",\n\s*null,\n)\s*"course\/c1",\n/g, '$1');
content = content.replace(/(\n\s*Guid\.NewGuid\(\),\n\s*Guid\.NewGuid\(\),\n\s*"Test",\n\s*null,\n)\s*"course\/c1",\n/g, '$1');
content = content.replace(/(\n\s*Guid\.NewGuid\(\),\n\s*Guid\.NewGuid\(\),\n\s*"Original Title",\n\s*"Original Desc",\n)\s*"course\/c1",\n/g, '$1');

// Remove doc.Scope assertion
content = content.replace(/\s*doc\.Scope\.Should\(\)\.Be\("course\/c1"\);\n/g, '\n');

fs.writeFileSync(docTestsPath, content);
console.log('Fixed DocumentTests.cs');

const uploadDocPath = 'tests/Modules/Documents/Documents.UnitTests/Commands/UploadDocumentTests.cs';
let uploadContent = fs.readFileSync(uploadDocPath, 'utf8');
uploadContent = uploadContent.replace(/\s*doc\.Scope\.Should\(\)\.Be\("course\/c1"\);\n/g, '\n');
fs.writeFileSync(uploadDocPath, uploadContent);
console.log('Fixed UploadDocumentTests.cs');

