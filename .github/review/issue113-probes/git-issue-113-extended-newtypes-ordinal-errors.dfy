// RUN: %exits-with 4 %verify --type-system-refresh true --general-newtypes true --extended-newtype-bases true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Ord = ORDINAL
newtype FiniteOrd = o: ORDINAL | o.IsNat witness 0
newtype PositiveOrd = o: ORDINAL | 0 < o witness 1
newtype AtMostOne = o: ORDINAL | o <= 1 witness 0

lemma FiniteCastNeedsProof(o: ORDINAL)
{
  var n := o as Ord;
  var i := n as int; // ERROR: ordinal-to-integer requires n.IsNat
}

lemma BadTarget(o: ORDINAL)
  requires !o.IsNat
{
  var n := o as FiniteOrd; // ERROR: target predicate is checked on o itself
}

lemma NegativeInteger()
{
  var n := (-1) as Ord; // ERROR: negative integers are not ordinals
}

function SubtractionNeedsNatural(x: Ord, y: Ord): Ord
  requires y.Offset <= x.Offset
{
  x - y // ERROR: y.IsNat is missing
}

function SubtractionNeedsOffset(x: Ord, y: Ord): Ord
  requires y.IsNat
{
  x - y // ERROR: offset underflow precondition is missing
}

lemma ArithmeticMustRecheck()
{
  var one := (1 as ORDINAL) as AtMostOne;
  var two := one + one; // ERROR: result violates AtMostOne
}

lemma SiblingCast()
{
  var zero := (0 as ORDINAL) as Ord;
  var positive := zero as PositiveOrd; // ERROR: destination predicate fails
}

lemma FiniteVacuity()
{
  var n := (0 as ORDINAL) as Ord;
  assert n.IsNat && n.Offset == 0;
  assert false; // ERROR: satisfiable carrier / membership control
}

lemma ArbitraryVacuity(o: ORDINAL)
{
  var n := o as Ord;
  assert (n as ORDINAL) == o;
  assert false; // ERROR: arbitrary parameter must not become inconsistent
}
