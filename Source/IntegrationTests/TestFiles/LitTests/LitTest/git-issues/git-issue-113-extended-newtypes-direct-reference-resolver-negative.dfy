// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

class C {}
trait T {}
type CAlias = C
type NullableCAlias = C?
type TAlias = T?
newtype DirectClass = C witness *
newtype NullableClass = C? witness *
newtype ClassAlias = CAlias witness *
newtype NullableClassAlias = NullableCAlias witness *
newtype DirectTrait = T? witness *
newtype TraitAlias = TAlias witness *
