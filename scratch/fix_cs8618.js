const fs = require('fs');

function fix(file, regex, replacement) {
    let content = fs.readFileSync(file, 'utf8');
    content = content.replace(regex, replacement);
    fs.writeFileSync(file, content);
}

// 1. CourseMediaAsset.cs -> Asset
// Wait, the log says CourseMediaAsset.cs? Actually the log says:
// CurriculumResource.cs(13,13): warning CS8618: Non-nullable property 'Asset'
fix('src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/CurriculumResource.cs', 
    /public CourseMediaAsset Asset \{ get; private set; \}/g,
    'public CourseMediaAsset Asset { get; private set; } = null!;');

// 2. Enrollement.cs -> Progress
fix('src/alphazero-api/Modules/Courses/Domain/Aggregates/Enrollements/Enrollement.cs',
    /public Progress Progress \{ get; private set; \}/g,
    'public Progress Progress { get; private set; } = null!;');

// 3. CourseOverview.cs -> ETag
fix('src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/CourseOverview.cs',
    /public string ETag \{ get; private set; \}/g,
    'public string ETag { get; private set; } = null!;');

// 4. Course.cs -> Title
fix('src/alphazero-api/Modules/Courses/Domain/Aggregates/Courses/Course.cs',
    /public string Title \{ get; private set; \}/g,
    'public string Title { get; private set; } = null!;');

// 5. TenantPrincipalAssignment.cs -> Principal, Resource
fix('src/alphazero-api/Modules/Identity/Domain/Models/TenantPrincipalAssignment.cs',
    /public Principal Principal \{ get; private set; \}/g,
    'public Principal Principal { get; private set; } = null!;');

fix('src/alphazero-api/Modules/Identity/Domain/Models/TenantPrincipalAssignment.cs',
    /public ResourceArn Resource \{ get; private set; \}/g,
    'public ResourceArn Resource { get; private set; } = null!;');

console.log('Fixed CS8618 warnings');
