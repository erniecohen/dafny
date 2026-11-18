// With --additional-axioms alone, a trait-typed field of an inductive datatype value is below it,
// which the default order sees through a downcast.
trait T {}

datatype L extends T = Nil | Cons(next: T)

function Length(x: L): nat decreases x {
  if x.Cons? && x.next is L then 1 + Length(x.next as L) else 0
}

lemma Below(x: L)
  requires x.Cons? && x.next is L
  ensures x.next as L < x
{
}

// Vacuity control
lemma False(x: L) ensures false {
  if x.Cons? && x.next is L {
    False(x.next as L);
  }
}
