// A predicate cannot quantify over all values of a general arrow: they may capture references, so the
// set of values depends on the set of allocated references.
ghost predicate AllReadNothing() {
  forall g: () ~> int :: g.reads() == {}
}
