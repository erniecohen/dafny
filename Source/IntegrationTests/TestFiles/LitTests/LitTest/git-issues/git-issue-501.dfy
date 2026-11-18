// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --member-invariants "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --general-newtypes --member-invariants "%S/Inputs/git-issue-501-invalid.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// A member witness satisfies the inhabitation obligation, including the base constraint.
// The reachable assertion is the only failure in this program.
newtype NZ = x: int | x != 0 witness 1
newtype NZ2 = NZ {
  invariant this as int >= 2
  witness 2
}
newtype Header = x: NZ | x as int >= 2 witness 2
newtype Ghost = NZ {
  invariant this as int >= 2
  ghost witness 2
}
method Reachable() {
  var x := 2 as NZ2;
  assert x as int >= 2;
  assert false;
}
