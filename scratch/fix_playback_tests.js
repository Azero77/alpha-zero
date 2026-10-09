const fs = require('fs');

const path = 'tests/Modules/VideoUploading/VideoUploading.Tests.Integration/CreatePlaybackSessionHandlerUnitTests.cs';
let content = fs.readFileSync(path, 'utf8');

content = content.replace(/CreatePlaybackSessionCommandHandler/g, 'CreatePlaybackSessionQueryHandler');
content = content.replace(/CreatePlaybackSessionCommandValidator/g, 'CreatePlaybackSessionQueryValidator');
content = content.replace(/CreatePlaybackSessionCommand/g, 'CreatePlaybackSessionQuery');

fs.writeFileSync(path, content);
