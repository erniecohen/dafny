// Unexecuted clean anti-vacuity control with separately proved Even introductions.
type Even = x: int | x % 2 == 0 witness 0
codatatype EvenStream = Cons(head: Even, tail: EvenStream)
function EvenValues(n: Even): EvenStream { Cons(n, EvenValues((n + 2) as Even)) }
lemma Head(n: Even)
  ensures EvenValues(n).head == n
{}
lemma InhabitedControl() {
  var value := EvenValues(0);
  assert value.head == 0;
  assert value.head % 2 == 0;
  assert false; // Intended sole verification failure, not yet measured.
}
