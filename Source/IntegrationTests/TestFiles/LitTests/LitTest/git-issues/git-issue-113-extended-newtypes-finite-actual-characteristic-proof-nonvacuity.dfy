// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --general-traits=datatype --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
