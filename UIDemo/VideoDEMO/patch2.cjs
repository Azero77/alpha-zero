const fs = require('fs');
const file = 'src/infrastructure/player/hls-player-impl.ts';
let code = fs.readFileSync(file, 'utf8');
code = code.replace("import { config } from '../../core/config';", "import { config as appConfig } from '../../core/config';");
code = code.replace(/config\.tenantId/g, 'appConfig.tenantId');
code = code.replace(/config\.authToken/g, 'appConfig.authToken');
code = code.replace('return new Request(context.url, initParams);', 'return undefined as any;');
fs.writeFileSync(file, code);
