// Input of git-issue-466-stage3-rejected.dfy: a trait of one member of a recursive module group
// that extends a reference trait of another member is a reference type, so a datatype may not
// extend it; a trait that extends a trait of another member that is not a reference type is not
// one.

module A {
  import B
  trait U extends B.T { }
  class K extends U { }
  method M(u: U?, k: K?) {
    var o: object? := u;
  }
  trait N extends B.V { }
  datatype D extends N = D
  datatype E extends U = E
}
module B {
  import A
  trait T extends object { }
  trait V { }
  trait W extends A.U { }
  method Q(w: W?) { }
}
