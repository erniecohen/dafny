// Part of github-issue-481.dfy: a client of Inputs/github-issue-481-library.dfy, given as a source
// library or as a .doo file.
module Client {
  import Lib
  import LibOpaque
  method Main() {
    var r := Lib.Make<() ~> bool>(() => true); // error
    var s := LibOpaque.Make<() ~> bool>(() => true); // error
  }
  method Put(t: Lib.Tr, a: array<() ~> bool>) modifies a requires a.Length > 0 {
    t.Put(a, () => true); // error
  }
  method Fine(t: Lib.Tr, a: array<int -> int>) modifies a requires a.Length > 0 {
    var r := Lib.Make<int -> int>(x => x);
    t.Put(a, x => x + 1);
  }
}
