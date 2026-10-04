module Provider {
  type Opaque = int
  newtype Wrapped = int witness 0
  export API reveals Opaque, Wrapped
}
module Client {
  import P = Provider`API
  newtype Outer = P.Wrapped witness 0
  type RevealedAlias(!new) = P.Opaque
  type RevealedNewtype(!new) = P.Wrapped
  type RevealedOuter(!new) = Outer
}
