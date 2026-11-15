// A for loop without a body is assumed to keep its invariant.
module Bad {
  method Bad()
    ensures false
  {
    for i := 0 to 2
      invariant i == 0
  }
}
