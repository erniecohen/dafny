// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --general-traits=datatype --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.off.expect" "%t"
// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --general-traits=datatype --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.on.expect" "%t"

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
