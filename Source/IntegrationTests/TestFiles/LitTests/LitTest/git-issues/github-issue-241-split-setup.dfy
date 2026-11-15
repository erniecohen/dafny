// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false --solver-path "%review-z3" --cores:1 --isolate-assertions --solver-log "%t.off.smt2" "%s" > "%t.off"
// RUN: %exits-with 4 %baredafny verify --use-basename-for-filename --show-snippets:false --solver-path "%review-z3" --cores:1 --isolate-assertions --solver-log "%t.on.smt2" --boogie "/cacheUsedTypes /lockFreeExpansion" "%s" > "%t.on"
// RUN: %diff "%s.expect" "%t.off"
// RUN: %diff "%t.off" "%t.on"
// RUN: %diff "%t.off.smt2" "%t.on.smt2"

// Boogie v3.5.5+review.c5a9f2ef (issue 241) adds two opt-in options that take work out of
// setting up a prover for each split: /cacheUsedTypes and /lockFreeExpansion.  Neither may
// change what the solver gets.  With every assertion its own split, the SMT-Lib text sent to
// the solver and the report are the same with both options as without them.

module A {
  datatype Tree = Leaf | Node(left: Tree, value: int, right: Tree)

  function Size(t: Tree): nat {
    match t
    case Leaf => 0
    case Node(l, _, r) => Size(l) + 1 + Size(r)
  }

  lemma SizeOfNode(l: Tree, v: int, r: Tree)
    ensures Size(Node(l, v, r)) > Size(l)
    ensures Size(Node(l, v, r)) > Size(r)
  {
  }
}

module B {
  import opened A

  function Sum(s: seq<int>): int {
    if |s| == 0 then 0 else s[0] + Sum(s[1..])
  }

  method Collect(m: map<int, bool>, xs: set<real>, t: Tree) returns (n: nat)
    ensures n == Size(t)
  {
    var bv: bv8 := 3;
    assert bv + 1 == 4;
    assert Sum([1, 2]) == 3;
    assert 0.5 in xs ==> xs != {};
    n := Size(t);
  }

  lemma Wrong(s: seq<int>)
    ensures Sum(s) >= 0  // fails: s may hold negative numbers
  {
  }
}
