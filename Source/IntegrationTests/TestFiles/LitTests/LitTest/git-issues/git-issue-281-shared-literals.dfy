// RUN: %exits-with 4 %verify --isolate-assertions --boogie "/typeEncoding:m /monomorphicSharedLiterals:1" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%S/Inputs/issue-281-shared-literals.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir"

// Both branches assign the same shared Boolean definite-assignment tracker.
// The option must reach Boogie's in-memory monomorphizer: serializing and
// parsing a .bpl file would lose the expression-object sharing being tested.
function Identity<T>(x: T): T { x }

method Join(b: bool) returns (x: int)
  ensures x == 0 || x == 1
{
  if b {
    x := 0;
  } else {
    x := 1;
  }
  assert Identity(x) == x;
}

method Uninitialized(b: bool) returns (x: int) {
  if b {
    x := 1;
  }
}

method FalseAtJoin(b: bool) {
  var x := 0;
  if b { x := 1; } else { x := 2; }
  assert Identity(x) == x;
  assert false;
}
