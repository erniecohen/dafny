// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted draft. Recursive newtype semantics are outside the feature scope.

codatatype Ring<T> = Ring(value: T, next: WrappedRing<T>)
newtype WrappedRing<T> = Ring<T> witness *

function RingRepeat<T>(x: T): WrappedRing<T> {
  Ring(x, RingRepeat(x)) as WrappedRing<T>
}
