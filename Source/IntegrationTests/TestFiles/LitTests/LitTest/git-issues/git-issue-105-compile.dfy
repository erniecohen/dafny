// RUN: %run --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %run --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --target py "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the default value of a datatype
// with invariants is its witness, compiled, so that every default the compiled program makes up (an
// array element, an out-parameter of a type parameter declared (0), a field of a class) satisfies the
// invariants.  A datatype without invariants whose constructor takes such a datatype gets its default
// from the witness too.  The witness of a generic datatype is compiled once and serves every
// instantiation.

datatype Interval = Interval(lo: int, hi: int) {
  invariant lo <= hi
  witness Interval(5, 7)
  function Width(): nat { hi - lo }
}
datatype Holder = Holder(i: Interval, n: int)

datatype Opt<X> = None | Some(x: X, n: int) {
  invariant Some? ==> n > 0
  witness Empty()
  static function Empty(): raw Opt<X> { None }
}
datatype Bag<X> = Bag(xs: seq<X>, n: int) {
  invariant n > |xs|
  witness Bag([], 3)
}
datatype Pair<Y> = Pair(o: Opt<Y>, b: Bag<Y>, k: int)

method Default<T(0)>() returns (t: T) { t := *; }

class Cell<T(0)> {
  var x: T
  constructor () { }
}

method Main() {
  var a := new Interval[2];
  var b := new Holder[1];
  print a[0], " ", a[1].Width(), " ", b[0], "\n";
  var t := Default<Interval>();
  var u := Default<Holder>();
  print t, " ", u, "\n";
  var c := new Cell<Interval>();
  print c.x, "\n";
  var s: seq<Interval> := seq(2, _ => Interval(1, 2));
  print s, "\n";
  var g := new Bag<int>[2];
  var p := new Pair<real>[1];
  print g[0], " ", g[1].n, " ", p[0], "\n";
  var bt := Default<Bag<bool>>();
  var pt := Default<Pair<char>>();
  print bt, " ", pt, "\n";
  var gc := new Cell<Bag<string>>();
  print gc.x, "\n";
}
