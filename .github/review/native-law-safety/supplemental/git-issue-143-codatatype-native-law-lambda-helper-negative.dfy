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

// This has the exact ordinary-helper / productive-actual / lambda shape.
// The field 0 as Positive is invalid in a mathematically inhabited carrier.
// No false assertion relies on the invalid producer's assumptions.
function Bad(): Stream {
  Cons(Bad(), () => Carry(Bad()), 0 as Positive)
}
