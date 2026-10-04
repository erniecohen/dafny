// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true --extended-newtype-bases=true --additional-axioms=false
// Universal controls; no sampled runtime witnesses and no recursive newtype declaration.
newtype Id<T> = T witness *
datatype Growing<T> = End | Step(next: Growing<seq<T>>)
newtype Wrapped<T> = Growing<Id<T>>
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
