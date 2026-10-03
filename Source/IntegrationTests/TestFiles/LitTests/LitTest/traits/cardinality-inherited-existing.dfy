// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Ordinary inherited variance checking already rejects this definition. This
// preserves that behavior and does not count as a new validator rejection.
trait H<!T> extends object { ghost var p: T -> bool }
class G<X> extends H<X> {}
