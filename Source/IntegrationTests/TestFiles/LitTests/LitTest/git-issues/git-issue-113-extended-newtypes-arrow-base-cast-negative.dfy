// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// A conversion into a newtype whose base is an arrow type must establish that
// the original function inhabits the instantiated arrow family (here the
// codomain nat), not only the totality and reads constraints. Each conversion
// below starts from a function outside that family and must be rejected.

newtype F = int -> nat witness *
newtype Outer = F witness *
newtype G<T> = int -> T witness *

function Use(f: int -> nat): nat { f(0) }

lemma LambdaLiteral() ensures false {
  var g := ((x: int) => -1) as F; // error
  var h := g as int -> nat;
  assert Use(h) == -1;
}

lemma LocalLambda() ensures false {
  var l: int -> int := (x: int) => -1;
  var g := l as F; // error
  var h := g as int -> nat;
  assert Use(h) == -1;
}

method Parameter(f: int -> int) {
  var g := f as F; // error
}

method NestedChain() {
  var g := ((x: int) => -1) as Outer; // error
}

method GenericActualIntToNat() {
  var g := ((x: int) => -1) as G<nat>; // error
}

method NarrowerDomain(f: nat -> nat) {
  var g := f as F; // error
}

// Nonvacuity: a valid conversion does not make the rest of the body vacuous.
method ValidThenFalse() {
  var g := ((x: int) => 1) as F;
  assert false; // error
}
