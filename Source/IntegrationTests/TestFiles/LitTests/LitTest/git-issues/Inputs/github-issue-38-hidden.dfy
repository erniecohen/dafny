module Hidden {
  export provides S
  datatype D = Value(o: ORDINAL)
  type S(!new) = D
}
module Client {
  import Hidden
  least predicate P(s: Hidden.S) {
    forall t: Hidden.S :: P(t)
  }
}
