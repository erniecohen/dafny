// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): a client class
// that inherits a library's trait axiom.

module Client {
  import Library
  @AssumeCrossModuleTermination
  class C extends Library.T<int> {
    ghost function Size(x: int): nat { if x < 0 then 1 - x else x + 1 }
  }
  lemma Use(c: C, x: int) ensures c.Size(x) > 0 { c.SizePositive(x); }
  lemma Control(c: C, x: int) { c.SizePositive(x); assert c.Size(x) > 0; assert false; }  // error
}
