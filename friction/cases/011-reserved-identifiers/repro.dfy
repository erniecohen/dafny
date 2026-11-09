// `assigned` and `var` are reserved words, so neither can name a declaration.
predicate assigned(x: int) { x > 0 }
function var(): int { 0 }
