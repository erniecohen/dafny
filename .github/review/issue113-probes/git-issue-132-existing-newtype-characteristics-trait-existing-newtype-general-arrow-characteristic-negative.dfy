// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
// Direct existing-language control plus nominal versions; general arrows may capture references.
newtype Id<T> = T witness *
newtype SeqId<T> = seq<T>
type WrongDirect(!new) = seq<int ~> int>
type WrongSequence(!new) = SeqId<int ~> int>
type WrongIdentity(!new) = Id<int ~> int>
