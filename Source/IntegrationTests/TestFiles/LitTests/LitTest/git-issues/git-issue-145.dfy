// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head: T, tail: Stream<T>)

// This program used to put logical negation in an automatic induction trigger.
lemma PrefixViews(s: Stream<int>, t: Stream<int>, k: nat)
  ensures (s ==#[k] t) <==> ((s as Stream<int>) ==#[k] (t as Stream<int>))
  ensures (s !=#[k] t) <==> ((s as Stream<int>) !=#[k] (t as Stream<int>))
{}

lemma {:induction false} NoInduction(s: Stream<int>, t: Stream<int>, k: nat)
  ensures (s !=#[k] t) <==> ((s as Stream<int>) !=#[k] (t as Stream<int>))
{}

lemma DistinctHeads(s: Stream<int>, t: Stream<int>)
  requires s.head != t.head
  ensures s !=#[1] t
{}
