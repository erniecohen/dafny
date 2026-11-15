// RUN: %baredafny verify "%s" --type-system-refresh --general-newtypes --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%review-z3" > "%t"
// RUN: %baredafny verify "%s" --type-system-refresh --general-newtypes --additional-axioms --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%review-z3" >> "%t"
// RUN: %exits-with 2 %baredafny verify "%S/Inputs/git-issue-82-newtypes-set-negative.dfy" --type-system-refresh --general-newtypes --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%review-z3" >> "%t"
// RUN: %exits-with 2 %baredafny verify "%S/Inputs/git-issue-82-newtypes-map-negative.dfy" --type-system-refresh --general-newtypes --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%review-z3" >> "%t"
// RUN: %exits-with 2 %baredafny verify "%S/Inputs/git-issue-82-newtypes-rank-negative.dfy" --type-system-refresh --general-newtypes --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%review-z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-newtypes-live-negative.dfy" --type-system-refresh --general-newtypes --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%review-z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-newtypes-live-negative.dfy" --type-system-refresh --general-newtypes --additional-axioms --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%review-z3" >> "%t"
// RUN: %diff "%s.expect" "%t"

newtype WrappedSet<T(!new)> = s: set<T> | true
newtype NestedSet<T(!new)> = s: WrappedSet<T> | true
newtype WrappedMap<K(!new), V(!new)> = m: map<K, V> | true
newtype NestedMap<K(!new), V(!new)> = m: WrappedMap<K, V> | true

ghost function WrappedExactImage(n: int): NestedSet<int> {
  set i: int | i == n :: i + 1
}

ghost function WrappedCapturedMap(n: int): NestedMap<int, int> {
  map i: int | i == n :: 0 := i
}

// Finite keys still admit arbitrary integer values.
ghost function WrappedBooleanMap(v: int): NestedMap<bool, int> {
  map b: bool | true :: v
}

ghost function WrappedIntegerWitness(i: int): int { i }

// The ancestor map's Boolean key carrier is finite even though its witness
// integers and its complete map-value carrier are infinite.
ghost function WrappedUnboundedBooleanMap(v: int): NestedMap<bool, int> {
  map i: int | true :: WrappedIntegerWitness(i) == 0 := v
}

lemma WrappedCollectionMembers(n: int, v: int) {
  assert WrappedExactImage(n) == {n + 1};
  var m := WrappedCapturedMap(n) as map<int, int>;
  assert m.Keys == {0};
  assert m[0] == n;
  assert (WrappedCapturedMap(n + 1) as map<int, int>)[0] == n + 1;
  var b := WrappedBooleanMap(v) as map<bool, int>;
  assert b.Keys == {false, true};
  assert b[false] == v;
  assert b[true] == v;
  var unbounded := WrappedUnboundedBooleanMap(v) as map<bool, int>;
  forall key: bool
    ensures key in unbounded
  {
    var sourceValue := if key then 0 else 1;
    assert WrappedIntegerWitness(sourceValue) == sourceValue;
    assert (WrappedIntegerWitness(sourceValue) == 0) == key;
    assert (WrappedIntegerWitness(sourceValue) == 0) in unbounded;
  }
  assert unbounded.Keys == {false, true};
  assert unbounded[false] == v;
  assert unbounded[true] == v;
}
