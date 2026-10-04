module Provider {
  type Pure(!new) = int
  newtype HiddenPure = Pure witness 0
  export API provides Pure, HiddenPure
}
module Client {
  import P = Provider`API
  newtype Id<T> = T witness *
  type AcceptDeclaredPromise(!new) = P.Pure
  type RejectHiddenNewtype(!new) = P.HiddenPure
  type RejectHiddenActual(!new) = Id<P.HiddenPure>
}
