// A client of github-issue-88-library.dfy that does not use the feature itself.

module Plain {
  import Lib1
  method Use() {
    var c := new Lib1.C();
    assert c.Size() == 2;
  }
}
