// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Id<T> = T witness *
type DirectPartial(!new) = int --> int
type DirectTotal(!new) = int -> int
type WrappedPartial(!new) = Id<int --> int>
type WrappedTotal(!new) = Id<int -> int>
type NestedPartial(!new) = Id<Id<int --> int>>
type NestedTotal(!new) = Id<Id<int -> int>>

lemma PurePartial(x: WrappedPartial)
  ensures forall f: WrappedPartial :: allocated(f)
{}
lemma PureTotal(x: WrappedTotal)
  ensures forall f: WrappedTotal :: allocated(f)
{}
