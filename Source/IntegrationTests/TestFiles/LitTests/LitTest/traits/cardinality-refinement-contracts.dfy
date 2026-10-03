// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module InvariantStrict { type T<X> }
module InvariantPermissive refines InvariantStrict { type T<!X> = X }
abstract module CovariantStrict { type T<+X> }
module CovariantPermissive refines CovariantStrict { type T<*X> = X }
