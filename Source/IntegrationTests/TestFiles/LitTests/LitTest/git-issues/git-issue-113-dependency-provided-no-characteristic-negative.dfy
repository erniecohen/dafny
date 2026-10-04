// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
