// Stored functions of https://github.com/erniecohen/dafny/issues/453 whose arguments do not reach the
// object without reading the heap.  They are accepted, and the "assert false" at the end is not.

datatype Pair = Pair(a: int, b: seq<int>)
datatype Box<X> = Box(x: X)
datatype Wrap = Wrap(f: int -> nat)
trait Ac<!I> { function Apply(i: I): nat }

// The class of another object, with a constant that does not reach C and a mutable field that does,
// which a function without a reads clause cannot read
class D {
  const n: int
  var c: C?
  constructor (n: int) { this.n := n; }
}

class FA<!I> extends Ac<I> {
  const f: I -> nat
  constructor (f: I -> nat) ensures this.f == f { this.f := f; }
  function Apply(i: I): nat { f(i) }
}

class G<!T> {
  const g: T -> nat
  constructor (g: T -> nat) ensures this.g == g { this.g := g; }
}

class C {
  const p: Pair -> nat
  const q: Box<int> -> nat
  const r: D -> nat
  const s: seq<Pair> -> nat
  const w: Wrap
  const a: Ac<int>
  const h: G<int>
  constructor (a: Ac<int>, h: G<int>) {
    p := (x: Pair) => |x.b|;
    q := (x: Box<int>) => if x.x < 0 then 0 else x.x;
    r := (d: D) => if d.n < 0 then 0 else d.n;
    s := (x: seq<Pair>) => |x|;
    w := Wrap((i: int) => if i < 0 then 0 else i);
    this.a := a;
    this.h := h;
  }
  function F(): nat { p(Pair(0, [])) + q(Box(1)) + w.f(2) + a.Apply(3) + h.g(4) }
}

// A nested datatype, whose recursion makes a larger instantiation at every step.  The search of N<int>
// went on through N<seq<int>>, N<seq<seq<int>>> and so on, and did not end.
datatype N<A> = Leaf(a: A) | Node(n: N<seq<A>>)

class Nested {
  const n: N<int>
  const m: N<int -> int>
  constructor () {
    n := Node(Leaf([1]));
    m := Leaf((i: int) => i);
  }
}

// A type nested in another instantiation of its own declaration, which is not a larger instantiation
class Self {
  const bb: Box<Box<Self?>>
  constructor () { bb := Box(Box(null)); }
}

method Main() {
  var fa := new FA<int>((i: int) => if i < 0 then 0 else i);
  var g := new G<int>((i: int) => 0);
  var c := new C(fa, g);
  assert fa.Apply(3) == 3;
  assert false;  // error: the stored functions do not prove false
}
