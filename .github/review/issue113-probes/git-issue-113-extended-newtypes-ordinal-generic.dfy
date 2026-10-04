// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun: valid methods must verify; every marked assertion/conversion must fail.

newtype Id<T> = T witness *
newtype Nonzero<T> = o: ORDINAL | o != 0 witness 1
newtype Reordered<A,B> = Nonzero<seq<B>>
type Alias<A,B> = Reordered<B,A>

lemma FiniteRepeatedDeclaration(o: ORDINAL) {
  var n := o as Id<Id<ORDINAL>>;
  assert (n as ORDINAL) == o;
}

lemma GoodGeneric(o: ORDINAL)
  requires o != 0
{
  var n := o as Alias<int,bool>;
  assert (n as ORDINAL) == o;
  assert n != (0 as ORDINAL) as Alias<int,bool>; // ERROR: explicit RHS bad target
}

lemma BadInnerConstraint() {
  var n := (0 as ORDINAL) as Alias<int,bool>; // ERROR: inner instantiated Nonzero constraint
}

lemma GenericVacuity() {
  var n := (1 as ORDINAL) as Alias<int,bool>;
  assert (n as ORDINAL) == 1;
  assert false; // ERROR: properly instantiated generic chain remains consistent
}
