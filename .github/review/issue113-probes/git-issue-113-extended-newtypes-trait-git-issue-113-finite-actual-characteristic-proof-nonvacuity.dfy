// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true --extended-newtype-bases=true --additional-axioms=false
newtype Id<T> = T witness *
datatype Growing<T> = End | Step(next: Growing<seq<T>>)
newtype Wrapped<T> = Growing<Id<T>>
type ReferenceFree(!new) = Wrapped<int>

lemma AllPureValues<T(!new)>()
  ensures forall value: T {:trigger allocated(value)} :: allocated(value)
{}

lemma PureRecursiveAllocationDoesNotProveFalse()
{
  AllPureValues<ReferenceFree>();
  assert false; // Must fail after the same full universal theorem is available.
}
