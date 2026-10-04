// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
// The type characteristic is the existing proof rule; no value-allocation premise is added.
newtype Id<T> = T witness *
type DirectPartial(!new) = int --> int
type DirectTotal(!new) = int -> int
type WrappedPartial(!new) = Id<int --> int>
type WrappedTotal(!new) = Id<int -> int>
type NestedPartial(!new) = Id<Id<int --> int>>
type NestedTotal(!new) = Id<Id<int -> int>>

lemma AllPureValues<T(!new)>()
  ensures forall value: T {:trigger allocated(value)} :: allocated(value)
{}

lemma PureDirectPartial()
  ensures forall f: DirectPartial {:trigger allocated(f)} :: allocated(f)
{
  AllPureValues<DirectPartial>();
}

lemma PureDirectTotal()
  ensures forall f: DirectTotal {:trigger allocated(f)} :: allocated(f)
{
  AllPureValues<DirectTotal>();
}

lemma PurePartial(x: WrappedPartial)
  ensures forall f: WrappedPartial {:trigger allocated(f)} :: allocated(f)
{
  AllPureValues<WrappedPartial>();
}

lemma PureTotal(x: WrappedTotal)
  ensures forall f: WrappedTotal {:trigger allocated(f)} :: allocated(f)
{
  AllPureValues<WrappedTotal>();
}

lemma PureNestedPartial()
  ensures forall f: NestedPartial {:trigger allocated(f)} :: allocated(f)
{
  AllPureValues<NestedPartial>();
}

lemma PureNestedTotal()
  ensures forall f: NestedTotal {:trigger allocated(f)} :: allocated(f)
{
  AllPureValues<NestedTotal>();
}
