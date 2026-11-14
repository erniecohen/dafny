// A bounded type has no default value: definite assignment applies, and a such-that statement must show a value exists.
module D {
  method Default() returns (e: bounded<nat, int -> int>) {
    var x: bounded<nat, int -> int>;
    e := x;
  }
  class C { var f: bounded<nat, int -> int>  constructor () { } }
  method Exists() { ghost var y: bounded<nat, int -> int> :| true; }
}
