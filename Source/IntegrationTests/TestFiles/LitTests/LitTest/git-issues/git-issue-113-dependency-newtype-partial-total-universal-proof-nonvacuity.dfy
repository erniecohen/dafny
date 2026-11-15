// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --general-traits=datatype --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --general-traits=datatype --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
