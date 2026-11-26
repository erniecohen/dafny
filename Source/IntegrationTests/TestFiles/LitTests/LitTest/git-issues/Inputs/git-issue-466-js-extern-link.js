const fs = require('fs');
const sources = process.argv.slice(2).map(file => fs.readFileSync(file, 'utf8'));
const program = sources.slice(0, 2).join('\n') + '\n{\n' + sources.slice(2).join('\n') + '\n}';
new Function('require', '"use strict";\n' + program)(require);
