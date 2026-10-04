// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Universal controls; no sampled runtime witnesses and no recursive newtype declaration.
newtype Id<T> = T
datatype Growing<T> = End | Step(next: Growing<seq<T>>)
newtype Wrapped<T> = Growing<T>
type FiniteNested(!new) = Id<Id<int>>
type ReferenceFree(!new) = Wrapped<int>
type PartialArrowFree(!new) = Wrapped<int --> int>
type TotalArrowFree(!new) = Wrapped<int -> int>

lemma FiniteRoundtrip(x: Id<Id<int>>)
  ensures ((x as Id<int>) as Id<Id<int>>) == x
{
}

lemma AllValuesAllocated(x: ReferenceFree)
  ensures allocated(x)
  ensures forall y: ReferenceFree :: allocated(y)
{
}

lemma ArrowValuesAllocated(x: TotalArrowFree, y: PartialArrowFree)
  ensures allocated(x) && allocated(y)
{
}
