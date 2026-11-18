// RUN: %exits-with 0 %baredafny resolve --type-system-refresh --contextual-lambda-domains --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" > "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh --contextual-lambda-domains --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path "%review-z3" "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 390 with subset types. The binder x takes the arrow's domain, while
// its pre-type prints as a synonym R of the subset type S. Resolution must not
// crash, and the verifier must still check that x is an S where g needs one.

type S = s: seq<int> | |s| > 0 witness [0]

type R = S

function f(): seq<int> -> bool { x => g(x) } // error: x need not be an S

function g(x: R): bool

// With S, or its synonym, as the declared domain, the call is well-formed.
function f2(): S -> bool { x => g(x) }

function f3(): R -> bool { x => g(x) }

// A subset type over a synonym, as domain and as formal.
type A = seq<int>
type T = t: A | |t| > 1 witness [0, 0]
type U = T

function f4(): T -> bool { x => g(x) }

function f5(): A -> bool { x => k(x) } // error: x need not be a T

function k(x: U): bool

function f6(): U -> bool { x => k(x) }
