trait Parent { }
datatype S extends Parent = N(o: ORDINAL) | Top
least predicate P(xs: iset<Parent>, s: Parent) {
  forall t: Parent :: t in xs ==> P(xs, t)
}
