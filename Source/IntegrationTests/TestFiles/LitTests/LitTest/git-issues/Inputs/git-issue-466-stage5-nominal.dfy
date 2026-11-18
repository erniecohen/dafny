abstract module A {
  import B
  datatype T = Leaf | Node(u: B.U)
}
abstract module B {
  import A
  datatype U = U(t: A.T)
}
abstract module R refines A { }
abstract module Client {
  import A
  import B
  import R
  method KeptSibling(t: R.T) requires t.Node? {
    var u: B.U := t.u;
    var original: A.T := u.t;
  }
  function MustNotRetie(t: R.T): B.U { B.U(t) }
}
