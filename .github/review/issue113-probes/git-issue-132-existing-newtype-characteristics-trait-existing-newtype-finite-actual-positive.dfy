// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
newtype Id<T> = T witness *
newtype SeqId<T> = seq<T>
type Pure(!new) = SeqId<Id<Id<int>>>

lemma PureValues()
  ensures forall values: Pure :: allocated(values)
{}

class Ref {}
newtype Project<L, R> = seq<R>
type PureUnusedArgument(!new) = Project<Ref?, int>
lemma PureUnusedValues()
  ensures forall values: PureUnusedArgument :: allocated(values)
{}
