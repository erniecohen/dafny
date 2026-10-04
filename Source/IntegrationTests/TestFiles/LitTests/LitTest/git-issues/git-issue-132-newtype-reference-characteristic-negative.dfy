// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// OMITTED-RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// OMITTED-RUN: %diff "%s.expect" "%t"

class Ref {}
newtype SeqId<T> = seq<T>
newtype Id<T> = T witness *
type WrongSequence(!new) = SeqId<Ref?>
type WrongNested(!new) = Id<SeqId<Ref?>>

newtype Project<L, R> = seq<R>
type WrongActualPosition(!new) = Project<int, Ref?>
