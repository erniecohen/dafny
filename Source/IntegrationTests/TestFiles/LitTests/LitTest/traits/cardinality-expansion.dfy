// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module TotalArrow { trait V {} datatype D extends V = D(p: V -> bool) }
module PartialArrow { trait V {} datatype D extends V = D(p: V --> bool) }
module ReadsArrow { trait V {} datatype D extends V = D(p: V ~> bool) }
module InfiniteSet { trait V {} datatype D extends V = D(ghost p: iset<V>) }
module InfiniteMapDomain { trait V {} datatype D extends V = D(ghost p: imap<V, int>) }
module DoubleNegative { trait V {} datatype D extends V = D(p: (V -> bool) -> bool) }
module NestedSequence { trait V {} datatype D extends V = D(p: seq<V -> bool>) }
module FiniteMapRange { trait V {} datatype D extends V = D(p: map<int, V -> bool>) }
module GhostField { trait V {} datatype D extends V = D(ghost p: V -> bool) }
module GhostConstructor { trait V {} datatype D extends V = Ground | ghost D(p: V -> bool) }
module IdentityAlias { trait V {} type F = V -> bool datatype D extends V = D(p: F) }
module PermissiveAlias { trait V {} type F<!X> = X -> bool datatype D extends V = D(p: F<V>) }
module TransitivePayload { trait V {} datatype B = B(p: V -> bool) datatype D extends V = D(b: B) }
module TwoTraits {
  trait V {} trait W {}
  datatype D extends V = D(p: W -> bool)
  datatype E extends W = E(d: D)
}
module TraitChain { trait V {} trait Middle extends V {} datatype D extends Middle = D(p: V -> bool) }
module CoDatatype { trait V {} codatatype D extends V = D(p: V -> bool) }
module EmptySubset {
  trait V {}
  type F = p: V -> bool | false witness *
  datatype D extends V = D(p: F)
}
module NominalHeadConservatism {
  trait V<T> {}
  datatype D extends V<bool> = D(p: V<int> -> bool)
}
