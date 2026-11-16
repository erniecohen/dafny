// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head: T, tail: Stream<T>)

lemma PrefixViews(s: Stream<int>, t: Stream<int>, k: nat)
  ensures (s ==#[k] t) <==> ((s as Stream<int>) ==#[k] (t as Stream<int>))
  ensures (s !=#[k] t) <==> ((s as Stream<int>) !=#[k] (t as Stream<int>))
{}

lemma {:induction false} NoInduction(s: Stream<int>, t: Stream<int>, k: nat)
  ensures (s !=#[k] t) <==> ((s as Stream<int>) !=#[k] (t as Stream<int>))
{}
