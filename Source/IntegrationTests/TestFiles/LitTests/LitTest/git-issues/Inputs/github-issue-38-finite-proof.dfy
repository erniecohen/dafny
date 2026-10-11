datatype S = Value(o: ORDINAL)
least predicate L(xs: set<S>, s: S) {
  s in xs || forall t: S :: t in xs ==> L(xs, t)
}
lemma Positive(s: S)
  ensures L({s}, s)
{
}
lemma FalseUnderAcceptedDefinition(s: S)
  requires L({s}, s)
  ensures false
{
}
