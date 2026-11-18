module InvLib {
  datatype Pos = Pos(n: int) {
    invariant n > 0
  }
}
