// Input of git-issue-466-stage3-rejected.dfy: as git-issue-466-stage3-lookup1.dfy, the other way
// around: the trait that the export set hides is the reference type.

module C {
  trait {:termination false} T { }
}
module A {
  import opened B
  import opened C
  trait U extends T { }
}
module B {
  export
    provides F, A
  import A
  trait T extends object { }
  function F(): int { 0 }
}
