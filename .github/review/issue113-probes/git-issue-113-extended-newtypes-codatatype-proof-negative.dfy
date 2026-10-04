// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted draft: each marked operation must retain its proof failure.

codatatype Stream = Cons(head: int, tail: Stream)
newtype Wrapped = Stream witness *

function Unguarded(): Wrapped {
  Unguarded() // no constructor guard
}

function Destructive(): Wrapped {
  Cons(0, Destructive() as Stream).tail as Wrapped // immediate destructor consumes guard
}

function DestructiveMatch(): Wrapped {
  Cons(0, match DestructiveMatch() case Cons(h, t) => t) as Wrapped
  // A match source must never be selected as a guarded co-call.
}

function WithPostcondition(): Wrapped
  ensures false
{
  Cons(0, WithPostcondition() as Stream) as Wrapped // explicit false postcondition cannot bootstrap
}

function InventedRank(s: Wrapped): int
  decreases s
{
  InventedRank(s.tail as Wrapped) // codata has no inductive structural rank
}

function Constant(): Stream { Cons(1, Constant()) }
newtype Positive = s: Stream | s.head > 0 ghost witness Constant()
method BadUpdate(s: Positive) {
  var t: Positive := s.(head := 0); // fails nominal reintroduction
}

method FeatureNonVacuity() {
  var s := Constant() as Wrapped;
  assert s.head == 1;
  assert false; // positive closed producer does not make the theory contradictory
}
