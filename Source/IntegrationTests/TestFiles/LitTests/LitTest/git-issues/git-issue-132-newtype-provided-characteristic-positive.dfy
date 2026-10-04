// RUN: %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// OMITTED-RUN: %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// OMITTED-RUN: %diff "%s.expect" "%t"

module Provider {
  type Pure(!new) = int
  type AbstractPure(!new)
  type GenericPure(!new)<T(!new)> = seq<T>
  export API provides Pure, AbstractPure, GenericPure
}
module Client {
  import P = Provider`API
  newtype WrapPure = P.Pure witness *
  newtype WrapAbstract = P.AbstractPure witness *
  newtype WrapGeneric = P.GenericPure<int> witness *
  newtype Id<T> = T witness *
  type AliasPromise(!new) = P.Pure
  type AbstractPromise(!new) = P.AbstractPure
  type GenericPromise(!new) = P.GenericPure<int>
  type WrappedPromise(!new) = WrapPure
  type WrappedAbstractPromise(!new) = WrapAbstract
  type WrappedGenericPromise(!new) = WrapGeneric
  type RepeatedPromise(!new) = Id<Id<P.Pure>>
  type CollectionPromise(!new) = seq<WrapPure>
}
