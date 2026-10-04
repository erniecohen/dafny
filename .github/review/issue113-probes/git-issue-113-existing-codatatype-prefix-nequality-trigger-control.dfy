// Existing-language control: no newtype declarations or extension-only operations.
// Unexecuted draft. Baseline diagnostics must establish classification.
codatatype Stream<T> = Cons(head: T, tail: Stream<T>)

lemma PrefixViews(s: Stream<int>, t: Stream<int>, k: nat)
  ensures (s ==#[k] t) <==> ((s as Stream<int>) ==#[k] (t as Stream<int>))
  ensures (s !=#[k] t) <==> ((s as Stream<int>) !=#[k] (t as Stream<int>))
{}

lemma {:induction false} NoInduction(s: Stream<int>, t: Stream<int>, k: nat)
  ensures (s !=#[k] t) <==> ((s as Stream<int>) !=#[k] (t as Stream<int>))
{}
