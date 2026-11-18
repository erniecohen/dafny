// Input of git-issue-466-stage3-rejected.dfy: a cycle of parent traits through two members of a
// recursive module group.

module A {
  import B
  trait U extends B.T { }
}
module B {
  import A
  trait T extends A.U { }
}
