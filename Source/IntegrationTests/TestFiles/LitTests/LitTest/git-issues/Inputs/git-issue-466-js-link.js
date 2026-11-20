// Link separately translated units with a shared producer scope and private client scopes.
// Each unit declares its own default module; the runtime is supplied by the producer.
const fs = require('fs');
const sources = process.argv.slice(2).map(path => fs.readFileSync(path, 'utf8'));
const program = sources.map((source, index) => index === 0 ? source : '{\n' + source + '\n}').join('\n');
new Function('require', program)(require);
