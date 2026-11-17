// Stored bounded values, compiled: a bound-zero function copied through generic identity, a mutable field and a
// sequence (prints 22 24 22); a heap-reading observer kept in a mutable field (prints 7); and a callback parameter
// kept by a lambda (prints 11).
type Zero = h: bounded<nat, int -> int> | h.bound == 0
  witness bounded.certify(0, (x: int) => x)
function Id<T>(x: T): T { x }
class Holder<T> {
  var f: T
  constructor (f: T) ensures this.f == f { this.f := f; }
}
function Twice(z: Zero, x: int): int decreases {:global} 1 { z(z(x)) }
class Counter {
  var n: int
  constructor () { n := 0; }
}
type Obs = h: bounded<nat, () ~> int> | h.bound == 0
  witness bounded.certify(0, () => 0)
method Capture(f: int -> int) returns (c: bounded<nat, () -> int>)
  decreases {:global} 9
  decreases {:callback f} 2
  ensures c.bound == 3
{
  c := bounded.certify(3, () => f(1));
}
method Main() decreases {:global} 10 {
  var inc: Zero := bounded.certify(0, (x: int) => x + 1);
  var h := new Holder(Id(inc));
  var s := [h.f, inc];
  var a := Twice(h.f, 20);
  h.f := bounded.certify(0, (x: int) => x + 2);
  print a, " ", Twice(h.f, 20), " ", Twice(s[0], 20), "\n";
  var c := new Counter();
  var o := new Holder<Obs>(bounded.certify(0, () reads c => c.n));
  c.n := 7;
  print o.f(), "\n";
  var k := Capture(x => x + 10);
  print k(), "\n";
}
