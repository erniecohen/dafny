// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
newtype Id<T> = T witness *
type WrappedTotal(!new) = Id<int -> int>

lemma AllPureValues<T(!new)>()
  ensures forall value: T {:trigger allocated(value)} :: allocated(value)
{}

lemma PureAllocationDoesNotImplyFalse()
{
  AllPureValues<WrappedTotal>();
  assert false; // Must fail even after importing the exact universal allocation theorem.
}

twostate lemma RawUnknownCapture(new f: () ~> int)
  ensures old(allocated(f))
{}

twostate lemma WrappedUnknownCapture(new f: Id<() ~> int>)
  ensures old(allocated(f))
{}
