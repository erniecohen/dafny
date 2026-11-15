// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Ring<T> = Ring(value: T, next: WrappedRing<T>)
newtype WrappedRing<T> = Ring<T> witness *

function RingRepeat<T>(x: T): WrappedRing<T> {
  Ring(x, RingRepeat(x)) as WrappedRing<T>
}
