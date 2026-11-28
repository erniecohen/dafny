// Capture the partially loaded exports object through an ordinary native cycle.
const captured = require('./issue466_required.js');
module.exports = { NativeCount(n) { return captured.__default.Count(n); } };
