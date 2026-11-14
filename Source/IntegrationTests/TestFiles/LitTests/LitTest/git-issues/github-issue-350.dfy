// RUN: echo "type-system-refresh:true" > "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --solver-path "%review-z3" --general-traits=datatype --allow-warnings:false --cores:1 --resource-limit:16000000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 --type-system-refresh:true >> "%t"
// RUN: echo "type-system-refresh:true --legacy-decreases-order" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --solver-path "%review-z3" --general-traits=datatype --allow-warnings:false --cores:1 --resource-limit:16000000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 --type-system-refresh:true --legacy-decreases-order >> "%t"
// RUN: echo "type-system-refresh:true --trait-rank-decreases" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --solver-path "%review-z3" --general-traits=datatype --allow-warnings:false --cores:1 --resource-limit:16000000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 --type-system-refresh:true --trait-rank-decreases >> "%t"
// RUN: echo "type-system-refresh:false" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --solver-path "%review-z3" --general-traits=datatype --allow-warnings:false --cores:1 --resource-limit:16000000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 --type-system-refresh:false >> "%t"
// RUN: echo "type-system-refresh:false --legacy-decreases-order" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --solver-path "%review-z3" --general-traits=datatype --allow-warnings:false --cores:1 --resource-limit:16000000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 --type-system-refresh:false --legacy-decreases-order >> "%t"
// RUN: echo "type-system-refresh:false --trait-rank-decreases" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --solver-path "%review-z3" --general-traits=datatype --allow-warnings:false --cores:1 --resource-limit:16000000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 --type-system-refresh:false --trait-rank-decreases >> "%t"
// RUN: echo "type-system-refresh:true general-traits=legacy (A is a reference type)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --solver-path "%review-z3" --general-traits=legacy --allow-warnings:false --cores:1 --resource-limit:16000000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 --type-system-refresh:true >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/350: comparing two values of a trait that is not a
// reference type (here, under --general-traits=datatype) in a decreases clause crashed the verifier.
// Without --trait-rank-decreases such values have no decreases order: a component of that type may
// only stay equal, so k reports "decreases clause might not decrease" instead of crashing, and the
// controls verify exactly when the trait-typed component is unchanged.  With --trait-rank-decreases
// they are compared by BoxRank, as before.

trait A {}

// The issue's program.
function k(x: A, y: A): int decreases x {
  k(y, x)
}

// The trait-typed component stays equal and the next one decreases: these verify.
function f(x: A, n: nat): int decreases x, n {
  if n == 0 then 0 else f(x, n - 1)
}

method M(x: A, n: nat) decreases x, n {
  if n != 0 { M(x, n - 1); }
}

method Loop(x: A, n: nat) {
  var y: A, i: nat := x, n;
  while i != 0 decreases y, i {
    i := i - 1;
  }
}

// The trait-typed component changes: these fail.
method BadLoop(x: A, z: A, n: nat) {
  var y: A, i: nat := x, n;
  while i != 0 decreases y, i {
    i := i - 1;
    y := z;
  }
}

method BadM(x: A, z: A) decreases x {
  BadM(z, x);
}
