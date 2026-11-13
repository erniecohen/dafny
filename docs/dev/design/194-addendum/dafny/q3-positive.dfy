// Proposed () syntax; expected outcomes are UNVERIFIED.
// --user-defined-operators --type-system-refresh
// All declarations except q3_false_control should verify; the control must
// fail at assert false. Main should print 7,5,9,11 on separate lines.

datatype Adder = Adder(n: int) {
  function {:operator "()"} apply(x: int): int { n + x }
  function {:operator "()"} value(): int { n }
}

datatype Curry = Curry(n: int) {
  function {:operator "()"} first(x: int): Adder { Adder(n + x) }
}

datatype StaticAdder = StaticAdder(n: int) {
  static function {:operator "()"} invoke(f: StaticAdder, x: int): int
  { f.n + x }
}

datatype FastAdder = FastAdder(n: int) {
  function {:operator "()"} run(x: int): int
  { n + x } by method { return n + x; }
}

datatype GhostAdder = GhostAdder(n: int) {
  ghost function {:operator "()"} run(x: int): int { n + x }
}

function ordinary(x: int): int { x + 1 }

lemma application_examples()
{
  var f := Adder(5);
  assert f(2) == f.apply(2) == 7;
  assert f() == f.value() == 5;
  assert Curry(4)(2)(3) == Curry(4).first(2).apply(3) == 9;
  assert StaticAdder(6)(5) == StaticAdder.invoke(StaticAdder(6), 5) == 11;
  assert FastAdder(7)(2) == 9;
  assert GhostAdder(8)(2) == 10;
  assert ordinary(3) == 4; // Existing named-function call is unchanged.
  var arrow := (x: int) => x + 1;
  assert arrow(3) == 4; // Existing arrow application is unchanged.
}

method Main()
{
  var f := Adder(5);
  print f(2), "\n", f(), "\n", Curry(4)(2)(3), "\n", StaticAdder(6)(5), "\n";
}

lemma q3_false_control()
{
  var f := Adder(5);
  assert f(2) == 7;
  assert false; // Must fail after a successful selected call.
}
