// RUN: %exits-with 2 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// A term of an explicit trigger that translates into something Boogie does not allow in
// a trigger (an equality, a comparison, a boolean operator, a quantifier or a lambda)
// is reported at the term, and its trigger is left out.  Dafny used to pass the trigger
// to Boogie, which failed to resolve the program, and dafny aborted.

datatype Kind = A | B | C
function key(x: nat): nat { x }

// The issue's program: "k in {A,B}" translates into "k == A || k == B".
function crash(k: Kind): set<nat> {
  set x: nat {:trigger key(x + (if k in {A,B} then 1 else 0))} | x < 1 :: x
}

// A comparison inside a quantifier's trigger.
lemma Comparison()
  ensures forall x: nat {:trigger key(if x < 3 then x else 0)} :: key(x) == x
{
}

// Valid triggers are unaffected: a conditional whose condition is a variable, and a
// conditional moved into a function, as the issue's workaround does.
function childDepth(k: Kind): nat { if k in {A,B} then 1 else 0 }

function fine(k: Kind, b: bool): set<nat> {
  set x: nat {:trigger key(x + (if b then 1 else 0))} | x < 1 :: x
}

function workaround(k: Kind): set<nat> {
  set x: nat {:trigger key(x + childDepth(k))} | x < 1 :: x
}

lemma Valid()
  ensures forall x: nat {:trigger key(x)} :: key(x) == x
{
}
