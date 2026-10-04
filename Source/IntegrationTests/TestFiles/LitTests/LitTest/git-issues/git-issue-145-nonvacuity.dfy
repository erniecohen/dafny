// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head: T, tail: Stream<T>)

// Normalizing the trigger must not erase the formula's logical negation.
lemma SameStreamInequality(s: Stream<int>)
  ensures s !=#[1] s
{}

// Exercise the formerly crashing automatic trigger in a consistent context.
lemma PrefixViewsCannotProveFalse(s: Stream<int>, t: Stream<int>, k: nat)
  ensures (s !=#[k] t) <==> ((s as Stream<int>) !=#[k] (t as Stream<int>))
{
  assert false;
}
