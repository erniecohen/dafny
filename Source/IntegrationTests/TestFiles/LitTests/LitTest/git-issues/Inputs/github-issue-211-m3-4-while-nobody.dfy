// A loop without a body is assumed to keep its invariant and to terminate.
module Bad {
  method Bad()
    ensures false
  {
    var i := 0;
    while i < 1
      invariant i == 0
  }
}
