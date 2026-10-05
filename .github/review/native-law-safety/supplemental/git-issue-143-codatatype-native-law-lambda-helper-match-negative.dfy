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
// The matched source is independently destructive. Matching must not supply
// full suspended-value membership or a productive-call permission.
function Bad(): Stream {
  Cons(Bad(), () => Carry((match Bad() case Cons(t, d, h) => t)),
    0 as Positive)
}
