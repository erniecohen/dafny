// RUN: echo "refresh=false axioms=false" > "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --type-system-refresh=false --additional-axioms=false --show-snippets=false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit=60 --cores=1 >> "%t"
// RUN: echo "refresh=false axioms=true" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --type-system-refresh=false --additional-axioms=true --show-snippets=false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit=60 --cores=1 >> "%t"
// RUN: echo "refresh=true axioms=false" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --type-system-refresh=true --additional-axioms=false --show-snippets=false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit=60 --cores=1 >> "%t"
// RUN: echo "refresh=true axioms=true" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --type-system-refresh=true --additional-axioms=true --show-snippets=false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit=60 --cores=1 >> "%t"
// RUN: %diff "%s.expect" "%t"

class C { constructor () {} }

method LaterProofCapture() {
  label L:
  var c := new C();
  var f := () => (assert true by { var x := c; assert x == c; } 0);
  assert old@L(allocated(f));
}

lemma InhabitedProofControl(c: C) ensures false {
  var f := () => (assert true by { var x := c; assert x == c; } 0);
  assert allocated(f);
  assert f() == 0;
}
