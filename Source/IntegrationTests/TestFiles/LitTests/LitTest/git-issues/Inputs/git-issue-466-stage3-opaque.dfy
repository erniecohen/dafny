// Input of git-issue-466-stage3-rejected.dfy: the export set of a member of a recursive module
// group only provides the trait, so another member may not extend it.

module A {
  import B
  trait U extends B.T { }
}
module B {
  export
    provides T, A
  import A
  trait T extends object { }
}
