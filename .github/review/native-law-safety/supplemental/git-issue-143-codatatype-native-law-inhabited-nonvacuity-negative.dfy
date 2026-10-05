// Source-only proposed exit 4. The sole final false assertion must fail.
// This source is independent of every invalid Bad producer.
// Valid assertions must succeed; exact counts/diagnostics are pending.
// Fresh BPL must contain the Capture lambda's scoped Carry#requires(s)
// ==> Carry#canCall(s) term. Capture/Carry must not be counted as covered
// merely from their source filenames.
type Positive = x: int | x > 0 witness 1
codatatype Stream = Cons(tail: Stream, delayed: () -> Stream, head: Positive)

// Direct guarded recursion, with no helper on the recursive edge.
function Good(): Stream {
  Cons(Good(), () => Good(), 1 as Positive)
}

function {:abstemious} Carry(s: Stream): Stream {
  Cons(s, () => s, s.head)
}

// This ordinary native helper call is the new theorem-only traversal leaf.
function Capture(s: Stream): () -> Stream {
  () => Carry(s)
}

// Universal contract over an arbitrary inhabited Stream, not one example.
// Capture applies Carry to precisely s, whose constructed head is s.head.
lemma CaptureHead(s: Stream)
  ensures Capture(s)().head == s.head
{
}

lemma InhabitedNonVacuity() {
  var g := Good();
  var delayed := Capture(g);
  CaptureHead(g);
  assert delayed().head == g.head;
  assert g.head == 1;
  assert false;
}
