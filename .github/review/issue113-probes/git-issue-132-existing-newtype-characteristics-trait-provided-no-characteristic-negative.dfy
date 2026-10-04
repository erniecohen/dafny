module Provider {
  type Opaque = int
  type AbstractOpaque
  export API provides Opaque, AbstractOpaque
}
module Client {
  import P = Provider`API
  newtype Wrap = P.Opaque witness *
  newtype WrapAbstract = P.AbstractOpaque witness *
  newtype Id<T> = T witness *
  type RejectDirect(!new) = P.Opaque
  type RejectAbstractDirect(!new) = P.AbstractOpaque
  type RejectWrapped(!new) = Wrap
  type RejectAbstractWrapped(!new) = WrapAbstract
  type RejectRepeated(!new) = Id<Id<P.Opaque>>
  type RejectCollection(!new) = seq<Wrap>
}
