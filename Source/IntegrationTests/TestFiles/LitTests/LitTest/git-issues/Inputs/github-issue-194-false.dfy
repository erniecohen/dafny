type Pos = x: int | 0 < x witness 1

datatype Rat = Rat(n: int, d: Pos) {
  function {:operator "+"} plus(b: Rat): Rat { Rat(n * b.d + b.n * d, d * b.d) }
  function {:operator "*"} times(b: Rat): Rat { Rat(n * b.n, d * b.d) }
  predicate {:operator "<="} le(b: Rat) { n * b.d <= b.n * d }
  predicate equiv(b: Rat) { n * b.d == b.n * d }
}

datatype Mat = Mat(a: int, b: int, c: int, d: int) {
  function {:operator "+"} plus(m: Mat): Mat {
    Mat(a + m.a, b + m.b, c + m.c, d + m.d)
  }
  function {:operator "*"} times(m: Mat): Mat {
    Mat(a * m.a + b * m.c, a * m.b + b * m.d,
        c * m.a + d * m.c, c * m.b + d * m.d)
  }
}

datatype Box<T> = Box(value: T) {
  static function {:operator "+"} second(a: Box<T>, b: Box<T>): Box<T> { b }
  function {:operator "-"} negate(): Box<T> { this }
  function {:operator "*"} repeat(n: int): Box<T> requires 0 <= n { this }
  function {:operator "/"} divide(n: int): Box<T> requires n != 0 { this }
  function {:operator "%"} remainder(n: int): Box<T> requires n != 0 { this }
  predicate {:operator "<="} step(b: Box<T>) { true }
  predicate {:operator ">="} back(b: Box<T>) { true }
}

class Counter {
  const value: int
  constructor(n: int) ensures value == n { value := n; }
  function {:operator "+"} plus(n: int): int { value + n }
}

datatype Rank = Leaf | Node(child: Rank) {
  predicate {:operator "<"} less(b: Rank) { true }
  predicate {:operator ">"} greater(b: Rank) { true }
}

function generic<T>(a: Box<T>, b: Box<T>): Box<T> { a + b }
function late(a: Box<int>): Box<int> { (var x := a; x) + a }

method Main() {
  var a := Rat(1, 2);
  var b := Rat(2, 3);
  var c := Rat(1, 6);
  var d := Rat(1, 2);
  assert a * b + c <= d; // NAMED: assert a.times(b).plus(c).le(d);
  assert Rat(1, 2).equiv(Rat(2, 4));
  assert Rat(1, 2) != Rat(2, 4);
  var m := Mat(1, 2, 3, 4);
  var n := Mat(0, 1, 1, 0);
  var i := Mat(1, 0, 0, 1);
  assert m * n + i == Mat(3, 1, 4, 4); // NAMED: assert m.times(n).plus(i) == Mat(3, 1, 4, 4);
  var x := Box(1);
  var y := Box(2);
  assert (x + y).value == 2; // NAMED: assert Box.second(x, y).value == 2;
  assert (-x).value == 1; // NAMED: assert x.negate().value == 1;
  assert (x * 3).value == 1; // NAMED: assert x.repeat(3).value == 1;
  assert (x / 1).value == 1; // NAMED: assert x.divide(1).value == 1;
  assert (x % 1).value == 1; // NAMED: assert x.remainder(1).value == 1;
  assert x <= y <= x; // NAMED: assert x.step(y) && y.step(x);
  assert x >= y; // NAMED: assert x.back(y);
  var counter := new Counter(4);
  assert counter + 2 == 6; // NAMED: assert counter.plus(2) == 6;
  assert Leaf < Node(Leaf);
  assert !(Leaf < Leaf);
  assert Node(Leaf) > Leaf;
  assert Leaf.less(Leaf);
  assert false; // FALSE_CONTROL
}
