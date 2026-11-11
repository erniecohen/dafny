datatype D = D {
  predicate {:operator "<"} less(b: D) { true }
}
method Main() { print D < D; }
