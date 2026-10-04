// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted draft: a lazy base default still needs a value for each non-ghost head.

codatatype Stream<T> = Cons(head: T, tail: Stream<T>)
newtype UnknownHeadDefault<T> = Stream<T>

// This witness check must fail: T has no known compiled default (and may be empty).
// Contrast Bare<T(0)> in the positive/runtime fixture, which uses the real base default.
