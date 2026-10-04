newtype Singleton<T> = s: seq<T> | |s| == 1 witness *
newtype Nested<U> = s: Singleton<seq<U>> | true witness *

lemma GoodNested(x: seq<seq<int>>)
  requires |x| == 1
{
  var n := x as Nested<int>;
  assert |n| == 1;
}

lemma BadNested()
{
  var x: seq<seq<int>> := [];
  var n := x as Nested<int>; // ERROR: inner Singleton<seq<int>> predicate fails
}

lemma NestedVacuity()
{
  var x: seq<seq<int>> := [[]];
  var n := x as Nested<int>;
  assert |n| == 1;
  assert false; // ERROR: instantiated target-chain reasoning is consistent
}
