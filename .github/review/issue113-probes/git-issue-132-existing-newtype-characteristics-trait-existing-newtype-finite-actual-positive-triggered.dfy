// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
// Exactly the two measured finite universal contracts, with explicit translated predicate triggers.
newtype Id<T> = T witness *
newtype SeqId<T> = seq<T>
type Pure(!new) = SeqId<Id<Id<int>>>

lemma PureValues()
  ensures forall values: Pure {:trigger allocated(values)} :: allocated(values)
{}

class Ref {}
newtype Project<L, R> = seq<R>
type PureUnusedArgument(!new) = Project<Ref?, int>
lemma PureUnusedValues()
  ensures forall values: PureUnusedArgument {:trigger allocated(values)} :: allocated(values)
{}
