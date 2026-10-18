// Controls for the body permissions of heap-free lambdas in every heap
// (--heap-free-lambda-facts).  Each method has exactly one obligation that must fail,
// marked, with the facts in play.

ghost function Idx(a: int): int { if 0 <= a < 7 then a else 0 }
function Partial(a: int): int requires 0 < a { a }

method RankVacuity() {
  var rank: int -> nat := (a: int) => 7 - Idx(a);
  assert rank(3) == 4;
  assert false; // fails
}

method RankWrongValue() {
  var rank: int -> nat := (a: int) => 7 - Idx(a);
  assert rank(3) == 5; // fails
}

// The stronger well-formedness check still checks the body's preconditions.
method PartialBody() {
  var f := (a: int) => Partial(a); // fails: precondition
}

method PartialBodyGuarded() returns (f: int --> int) {
  f := (a: int) requires 0 < a => Partial(a);
  assert f.requires(1);
  assert f.requires(0); // fails
}

// A lambda whose formal is a function, used under a quantifier.
datatype Sem = Sem(step: (int, int, int) -> bool)
datatype Ctx = Ctx(p: int, o: nat -> int)

ghost predicate StepS(x: Ctx, s: int, t: int, s': int) { s' == s + t + x.p }
ghost function Src(x: Ctx): Sem { Sem((s, t, s') => StepS(x, s, t, s')) }
ghost function AtOracle(x: Ctx, o: nat -> int): Ctx { x.(o := o) }
ghost function Fam(x: Ctx): (nat -> int) -> Sem { (o: nat -> int) => Src(AtOracle(x, o)) }

lemma FamilyVacuity(x: Ctx) {
  assert forall o: nat -> int, s, t, s' :: Fam(x)(o).step(s, t, s') <==> s' == s + t + x.p;
  assert false; // fails
}

lemma FamilyWrongStep(x: Ctx, o: nat -> int) {
  assert Fam(x)(o).step(0, 0, x.p + 1); // fails
}

// A lambda that captures a reference and reads nothing: the allocation repair
// (#137) still decides its allocation; the permissions do not.
class Cell {
  var v: int
  constructor () {}
}

method CapturedLaterNotAllocatedEarlier() {
  label L:
  var n := new Cell();
  var f := () => n;
  assert f() == n;
  assert old@L(allocated(f)); // fails
}
