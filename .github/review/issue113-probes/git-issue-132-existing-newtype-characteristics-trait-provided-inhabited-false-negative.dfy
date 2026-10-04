module Provider {
  type Pure(!new) = int
  function Zero(): Pure { 0 }
  export API provides Pure, Zero
}
module Client {
  import P = Provider`API
  newtype Wrap = P.Pure witness *
  type WrapPromise(!new) = Wrap
  lemma Inhabited() {
    var value := P.Zero() as Wrap;
    assert false;
  }
}
