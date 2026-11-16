// RUN: %exits-with 0 %baredafny resolve --type-system-refresh --contextual-lambda-domains --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" > "%t"
// RUN: %exits-with 0 %baredafny verify --type-system-refresh --contextual-lambda-domains --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path "%review-z3" "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 390 with a generic synonym for the formal.

type B<T> = seq<T>

function f(): seq<int> -> bool { x => g(x) }

function g(x: B<int>): bool

function f2(): B<int> -> bool { x => h(x) }

function h(x: seq<int>): bool

// A synonym that drops its argument into a nested position.
type P<T> = map<T, seq<T>>

function f3(): map<int, seq<int>> -> bool { m => k(m) }

function k(m: P<int>): bool
