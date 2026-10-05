// Source-only focused proposal: no verifier/compiler execution has occurred.
// Proposed verification exit: 4, with a real source rejection (not resource exhaustion).
// Exact verified/error counts and diagnostics are pending actual measurement.
// Observe fresh BPL: a native Carry#requires ==> Carry#canCall implication
// whose actual contains Bad(). Do not infer CoCall.Yes from the filename.
// The full ordinary consumer membership checks must remain present.

type Positive = x: int | x > 0 witness 1
codatatype Stream = Cons(tail: Stream, delayed: () -> Stream, head: Positive)
function {:abstemious} Carry(s: Stream): Stream {
  Cons(s, () => s, s.head)
}
// The exact-let binding is destructive in existing CoCallResolution.
// Retain its original termination/membership rejection; the new traversal
// must not turn the binding into recursive permission.
function Bad(): Stream {
  Cons(Bad(), () => Carry((var s := Bad(); s)), 0 as Positive)
}
