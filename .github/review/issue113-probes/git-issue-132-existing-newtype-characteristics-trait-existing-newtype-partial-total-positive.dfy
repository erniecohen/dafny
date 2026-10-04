// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
// Measure unchanged owner source first: these empty-reads families must remain reference-free.
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
