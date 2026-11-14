// Known interaction with https://github.com/erniecohen/dafny/issues/211: an override in another module of
// a trait marked {:termination false} is not checked for termination, so such a program can build a value
// below itself.  The rank fact for direct fields already proves false here (BadDirect, with or without
// --additional-axioms); the rank fact for arrow fields does the same (BadArrow, with --additional-axioms).
// Both verdicts move when issue 211 is fixed.
module A {
  datatype Tree = Leaf | Node(kids: nat -> Tree) | One(t: Tree)
  trait {:termination false} Tr { function Get(): nat -> Tree  function Get1(): Tree }
  function Use(t: Tr): Tree { Node(t.Get()) }
  function Use1(t: Tr): Tree { One(t.Get1()) }
}
module B {
  import opened A
  class C extends Tr {
    function Get(): nat -> Tree { (n: nat) => Use(this) }
    function Get1(): Tree { Use1(this) }
  }
  lemma BadDirect(c: C) ensures false { assert Use1(c) == One(Use1(c)); assert Use1(c) < One(Use1(c)); }
  lemma BadArrow(c: C) ensures false { assert Use(c) == Node(c.Get()); assert c.Get()(0) == Use(c); }
}
