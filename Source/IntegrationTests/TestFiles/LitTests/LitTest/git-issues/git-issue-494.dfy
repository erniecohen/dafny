// RUN: %verify --solver-path %review-z3 "%s" > "%t"
// RUN: %baredafny verify --solver-path %review-z3 --type-system-refresh=false --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path %review-z3 "%S/Inputs/git-issue-494-false.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Translation must retain the builtins of resolved members. The corresponding
// FormatterWorksForCppExample unit test also retains unresolved copies of
// members, as the formatter does, and checks that resolution preserves formatting.
module M {
  function F(x: bv77): bv77 { x }
  method Work() {
    var x := F(0);
    assert x == 0;
    var a := new int[2, 3]((i, j) => i + j);
    assert a[0, 0] == 0;
  }
}
