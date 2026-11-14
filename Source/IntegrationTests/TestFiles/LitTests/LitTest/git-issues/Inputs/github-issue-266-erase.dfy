// The compilers erase bounded<O, F> to F, with no wrapper, in signatures, fields, generic instantiations and maps.
// CHECK: public static void Pass(Func<BigInteger, BigInteger> c, Func<BigInteger, BigInteger> d, out Func<BigInteger, BigInteger> r, out Dafny.ISequence<Func<BigInteger, BigInteger>> s, out Func<BigInteger, BigInteger> b, out Holder h)
// CHECK: public static void Gen(Func<BigInteger, BigInteger> c, out Func<BigInteger, BigInteger> d, out Cell<Func<BigInteger, BigInteger>> e)
// CHECK: public Func<BigInteger, BigInteger> f {get; set;}
datatype Box<T> = Box(t: T)
class Holder {
  var f: bounded<nat, int -> int>
  constructor (f: bounded<nat, int -> int>) { this.f := f; }
}
method Pass(c: bounded<nat, int -> nat>, d: bounded<ORDINAL, int -> int>) returns (r: bounded<nat, int --> int>, s: seq<bounded<nat, int -> int>>, b: Box<bounded<ORDINAL, int -> int>>, h: Holder) {
  r := c;
  s := [c, c];
  b := Box(d);
  h := new Holder(c);
  h.f := s[1];
}
method Main() { print "erased\n"; }
function Id<T>(x: T): T { x }
class Cell<T> { var t: T  constructor (t: T) { this.t := t; } }
method Gen(c: bounded<nat, int -> int>) returns (d: bounded<nat, int -> int>, e: Cell<bounded<nat, int -> int>>) {
  d := Id(c);
  e := new Cell(d);
  var m: map<int, bounded<nat, int -> int>> := map[0 := c];
  d := m[0];
}
