// https://github.com/erniecohen/dafny/issues/307
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/Inputs/github-issue-194-ghost-formal-named.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.corpus-false.doo" > "%t.corpus-false.build"
// RUN: %baredafny resolve "%t.corpus-false.doo" --cores 1 --allow-warnings --type-system-refresh:false > "%t.corpus-false.readback"
// RUN: %baredafny build -t:lib "%S/Inputs/github-issue-194-ghost-formal-named.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.corpus-true.doo" > "%t.corpus-true.build"
// RUN: %baredafny resolve "%t.corpus-true.doo" --cores 1 --allow-warnings --type-system-refresh:true > "%t.corpus-true.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" > "%t.reduced-false.build"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false > "%t.reduced-false.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" > "%t.reduced-true.build"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true > "%t.reduced-true.readback"
// RUN: echo "Both resolver round-trips completed." > "%t"
// RUN: %diff "%s.expect" "%t"

// A parenthesized receiver whose name is also a type or module keeps its
// parentheses; other parenthesized receivers print as before.

datatype D = D {
  function plus(b: D): D { this }
  function nop(): D { this }
}
datatype E = A | B { function nop(): E { this } }
method Main() {
  var y := D;
  var x := (D).plus(y);
  var z := (D).nop();
  var w := (y).nop();
  var v := (A).nop();
  assert x == D;
}
