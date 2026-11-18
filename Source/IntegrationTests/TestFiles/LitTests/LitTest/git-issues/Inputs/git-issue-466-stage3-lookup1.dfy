// Input of git-issue-466-stage3-rejected.dfy: whether a trait is a reference type is decided
// before the imports among the members of a recursive module group are resolved, by looking up the
// names of its parent traits in all the declarations of the modules they are in.  Here that lookup
// finds two traits named T, one of them hidden by an export set, where the resolution of the name
// later finds the other one, which is a reference type.  The lookup and the resolution disagree,
// and that is an error.

module C {
  trait {:termination false} T extends object { }
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
  trait T { }
  function F(): int { 0 }
}
