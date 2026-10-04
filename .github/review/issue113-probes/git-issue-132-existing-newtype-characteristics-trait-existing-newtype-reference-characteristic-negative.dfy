// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
// Both aliases must reject (!new); these are representation references, not wrapper-owned references.
class Ref {}
newtype SeqId<T> = seq<T>
newtype Id<T> = T witness *
type WrongSequence(!new) = SeqId<Ref?>
type WrongNested(!new) = Id<SeqId<Ref?>>

newtype Project<L, R> = seq<R>
type WrongActualPosition(!new) = Project<int, Ref?>
