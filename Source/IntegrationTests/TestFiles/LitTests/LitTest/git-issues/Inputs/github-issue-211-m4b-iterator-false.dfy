include "github-issue-211-m4b-iterator.dfy"
module Controls {
  import Library
  method Use() decreases {:global} 0 {
    var i := new Library.Once();
    var more := i.MoveNext();
    assert false;
  }
}
