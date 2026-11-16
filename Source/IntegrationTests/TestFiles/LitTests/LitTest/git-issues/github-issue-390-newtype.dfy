// RUN: %exits-with 0 %baredafny resolve --type-system-refresh --contextual-lambda-domains --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" > "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh --contextual-lambda-domains --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path "%review-z3" "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 390 with a newtype domain: the binder takes the newtype N, while its
// pre-type prints as a synonym or a subset type of N.

newtype N = x: int | 0 <= x < 100

type M = N

function f(): N -> bool { x => g(x) }

function g(x: M): bool

type Small = n: N | n < 10

function f2(): N -> bool { x => k(x) } // error: x need not be Small

function k(x: Small): bool

function f3(): Small -> bool { x => k(x) }
