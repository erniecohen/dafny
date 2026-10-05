// Separate inhabited, valid nominal stream and observing-lambda context.
// No declaration or assumption from the invalid Bad program is imported.
type Positive = x: int | x > 0 witness 1
codatatype Stream = Cons(head: Positive, tail: Stream)
newtype Wrapped = Stream witness *

function RepeatOne(): Wrapped {
  Cons(1 as Positive, RepeatOne() as Stream) as Wrapped
}

lemma IndependentFalse() {
  var one := RepeatOne();
  assert one.head == (1 as Positive);
  var observer: () -> Positive := () => one.head;
  assert observer() == (1 as Positive);
  assert false;
}
