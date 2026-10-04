// RUN: ! %resolve "%s" --type-system-refresh=true --general-newtypes --extended-newtype-bases
// Direct reference and trait bases remain outside #113's capability scope.
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
