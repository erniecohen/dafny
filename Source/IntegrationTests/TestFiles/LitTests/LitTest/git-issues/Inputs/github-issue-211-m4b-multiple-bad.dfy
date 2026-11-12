include "github-issue-211-m4b-good.dfy"
module BadImplementation {
  import Library
  class C extends Library.Left, Library.Right {
    method L() decreases {:global} 1 {}
    method R() decreases {:global} 1 {}
  }
}
