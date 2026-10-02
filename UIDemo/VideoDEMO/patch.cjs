const fs = require('fs');
const file = 'src/infrastructure/player/hls-player-impl.ts';
let code = fs.readFileSync(file, 'utf8');
code = code.replace('return new Request(context.url, initParams);', 'return undefined as any;');
fs.writeFileSync(file, code);
