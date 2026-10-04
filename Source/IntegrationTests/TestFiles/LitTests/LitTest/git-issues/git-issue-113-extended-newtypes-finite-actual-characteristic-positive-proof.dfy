// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Id<T> = T witness *
datatype Growing<T> = End | Step(next: Growing<seq<T>>)
newtype Wrapped<T> = Growing<T>
type FiniteNested(!new) = Id<Id<int>>
type ReferenceFree(!new) = Wrapped<int>
type PartialArrowFree(!new) = Wrapped<int --> int>
type TotalArrowFree(!new) = Wrapped<int -> int>

lemma AllPureValues<T(!new)>()
  ensures forall value: T {:trigger allocated(value)} :: allocated(value)
{}

lemma FiniteRoundtrip(x: Id<Id<int>>)
  ensures ((x as Id<int>) as Id<Id<int>>) == x
{
}

lemma AllValuesAllocated(x: ReferenceFree)
  ensures allocated(x)
  ensures forall y: ReferenceFree {:trigger allocated(y)} :: allocated(y)
{
  AllPureValues<ReferenceFree>();
}

lemma ArrowValuesAllocated(x: TotalArrowFree, y: PartialArrowFree)
  ensures allocated(x) && allocated(y)
{
}
