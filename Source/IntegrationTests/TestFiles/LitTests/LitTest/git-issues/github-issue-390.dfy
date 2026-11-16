// RUN: %exits-with 0 %baredafny resolve --type-system-refresh --contextual-lambda-domains --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" > "%t"
// RUN: %exits-with 0 %baredafny verify --type-system-refresh --contextual-lambda-domains --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path "%review-z3" "%s" >> "%t"
// RUN: %exits-with 0 %baredafny verify --type-system-refresh --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path "%review-z3" "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Under --contextual-lambda-domains, the binder x takes the arrow's domain
// seq<int>, while its pre-type prints as the synonym A of g's formal.
// Combining the two used to cast seq<int> to a user-defined type and crash.

type A = seq<int>

function f(): seq<int> -> bool { x => g(x) }

function g(x: A): bool

// The other way round: the domain is the synonym, the formal is not.
function f2(): A -> bool { x => h(x) }

function h(x: seq<int>): bool

// Two different synonyms of the same type.
type A' = seq<int>

function f3(): A' -> bool { x => g(x) }
