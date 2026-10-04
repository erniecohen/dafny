// RUN: %baredafny verify "%s" --type-system-refresh:false --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" > "%t"
// RUN: %baredafny verify "%s" --type-system-refresh:true --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %baredafny verify "%s" --type-system-refresh:false --additional-axioms --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %baredafny verify "%s" --type-system-refresh:true --additional-axioms --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-mixed-negative.dfy" --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-empty-outer-negative.dfy" --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-spec-negative.dfy" --type-system-refresh:false --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-spec-negative.dfy" --type-system-refresh:false --additional-axioms --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-spec-negative.dfy" --type-system-refresh:true --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-spec-negative.dfy" --type-system-refresh:true --additional-axioms --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-lambda-alloc-negative.dfy" --type-system-refresh:false --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-lambda-alloc-negative.dfy" --type-system-refresh:false --additional-axioms --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-lambda-alloc-negative.dfy" --type-system-refresh:true --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-82-lambda-alloc-negative.dfy" --type-system-refresh:true --additional-axioms --show-snippets:false --use-basename-for-filename --allow-warnings --solver-path "%z3" >> "%t"
// RUN: %diff "%s.expect" "%t"

type Empty = x: int | false witness *

type Small = x: int | 0 <= x < 2 witness 0

datatype Enum = Left | Right

ghost function BooleanImage(): set<bool> { set i: int | true :: i == 0 }

ghost function EnumerationImage(): set<Enum> {
  set i: int | true :: if i == 0 then Left else Right
}

ghost function Identity<A(!new)>(xs: set<A>): set<A> { set x: A | x in xs }

// Source arguments containing references are allocated in their admitted heap.
// Shared function axioms must not promote arbitrary unallocated isets to finite sets.
ghost function ReferenceImage(xs: iset<object?>): set<object?> {
  set x: object? | x in xs
}

lemma ReferenceImageMembers(xs: iset<object?>) {
  var s := ReferenceImage(xs);
  assert forall o: object? :: o in s <==> o in xs;
}

lemma EmptyAndFiniteCarriers() {
  var empty := set e: Empty | 0 <= e < 1 :: e;
  assert empty == {};
  var image := set i: Small | true :: i + 10;
  forall j: int | j == 10 || j == 11
    ensures j in image
  {
    var sourceValue: Small := j - 10;
    assert sourceValue + 10 == j;
    assert sourceValue + 10 in image;
  }
  assert forall j: int | j in image :: j == 10 || j == 11;
  assert image == {10, 11};
  forall b: bool
    ensures b in BooleanImage()
  {
    var sourceValue := if b then 0 else 1;
    assert (sourceValue == 0) == b;
    assert (sourceValue == 0) in BooleanImage();
  }
  assert BooleanImage() == {false, true};
  forall e: Enum
    ensures e in EnumerationImage()
  {
    var sourceValue := if e.Left? then 0 else 1;
    assert (if sourceValue == 0 then Left else Right) == e;
    assert (if sourceValue == 0 then Left else Right) in EnumerationImage();
  }
  assert EnumerationImage() == {Left, Right};
  assert Identity({0, 1}) == {0, 1};
}

lemma StandardFiniteBounds(n: nat) {
  var interval := set i: int | 0 <= i < n;
  assert forall i: int :: i in interval <==> 0 <= i < n;
  var reversed := set i: int | n <= i < 0;
  assert reversed == {};
  var exact := set i: int | i == n :: i + 1;
  assert exact == {n + 1};
  var seqImage := set i: int | i in [0, 1, 1] :: i % 2;
  forall j: int | j == 0 || j == 1
    ensures j in seqImage
  {
    assert j in [0, 1, 1];
    assert j % 2 == j;
  }
  assert forall j: int | j in seqImage :: j == 0 || j == 1;
  assert seqImage == {0, 1};
  var multiImage := set i: int | i in multiset{0, 1, 1} :: i % 2;
  forall j: int | j == 0 || j == 1
    ensures j in multiImage
  {
    assert j in multiset{0, 1, 1};
    assert j % 2 == j;
  }
  assert forall j: int | j in multiImage :: j == 0 || j == 1;
  assert multiImage == {0, 1};
  var keyImage := set i: int | i in map[0 := 5, 1 := 6] :: i + 10;
  forall j: int | j == 10 || j == 11
    ensures j in keyImage
  {
    assert j - 10 in map[0 := 5, 1 := 6];
    assert (j - 10) + 10 == j;
  }
  assert forall j: int | j in keyImage :: j == 10 || j == 11;
  assert keyImage == {10, 11};
  var power := set t: set<int> | t <= {0, 1};
  forall t: set<int> | t <= {0, 1}
    ensures t == {} || t == {0} || t == {1} || t == {0, 1}
  {
    if 0 in t {
      if 1 in t { assert t == {0, 1}; }
      else { assert t == {0}; }
    } else {
      if 1 in t { assert t == {1}; }
      else { assert t == {}; }
    }
  }
  assert {} in power && {0} in power && {1} in power && {0, 1} in power;
  assert power == {{}, {0}, {1}, {0, 1}};
}

lemma DependentAndNestedBounds() {
  var pairs := set i: int, j: int | 0 <= i < 2 && i <= j < 2 :: (i, j);
  assert pairs == {(0, 0), (0, 1), (1, 1)};
  var reversedOrder := set j: int, i: int | 0 <= i < 2 && i <= j < 2 :: (i, j);
  assert reversedOrder == pairs;
  var nested := set i: int | 0 <= i < 2 :: (set j: int | 0 <= j < i + 1);
  forall i: int | 0 <= i < 2
    ensures (set j: int | 0 <= j < i + 1) == (if i == 0 then {0} else {0, 1})
  {}
  assert (set j: int | 0 <= j < 1) == {0};
  assert (set j: int | 0 <= j < 2) == {0, 1};
  var outerZero := 0;
  var outerOne := 1;
  assert (set j: int | 0 <= j < outerZero + 1) in nested;
  assert (set j: int | 0 <= j < outerOne + 1) in nested;
  assert {0} in nested && {0, 1} in nested;
  assert forall t: set<int> | t in nested :: t == {0} || t == {0, 1};
  assert nested == {{0}, {0, 1}};
  var infiniteOuter := iset i: int | true :: (set j: int | j == i);
  SingletonImage82(0);
  SingletonImage82(1);
  assert {0} in infiniteOuter;
  assert {1} in infiniteOuter;
}

lemma SingletonImage82(i: int)
  ensures (set j: int | j == i) == {i}
{}

lemma FiniteDomainWithInfiniteWitnesses() {
  var m := map i: int | true :: i == 0 := 0;
  forall b: bool
    ensures b in m
  {
    var sourceValue := if b then 0 else 1;
    assert (sourceValue == 0) == b;
    assert (sourceValue == 0) in m;
  }
  assert m.Keys == {false, true};
  assert m[false] == 0;
  assert m[true] == 0;
}

lemma FiniteMapDomainAndMapCarrier(v: int) {
  var m := map b: bool | true :: v;
  assert m.Keys == {false, true};
  assert m[false] == v;
  assert m[true] == v;
  var allBooleanMaps := set m: map<bool, bool> | true;
  assert map[true := false] in allBooleanMaps;
  assert map[false := true] in allBooleanMaps;
  var finiteMapImage := set i: int | 0 <= i < 2 :: map[true := i];
  assert finiteMapImage == {map[true := 0], map[true := 1]};
}

// Callers use only the opaque function contracts. These comprehensions occur in
// postconditions, so their definitions must be available in common consequence
// axioms, including when optional verification-only axioms are disabled.
opaque ghost function OpaqueSuccessor(n: int): (s: set<int>)
  ensures s == (set i: int | i == n :: i + 1)
{
  {n + 1}
}

opaque ghost function OpaqueConstantKey(n: int): (m: map<int, int>)
  ensures m == (map i: int | i == n :: 0 := i)
{
  map[0 := n]
}

lemma UsesOpaqueComprehensionContracts(n: int) {
  var s := OpaqueSuccessor(n);
  assert n + 1 in s;
  assert n !in s;
  var m := OpaqueConstantKey(n);
  assert m.Keys == {0};
  assert m[0] == n;
  var next := OpaqueConstantKey(n + 1);
  assert next[0] == n + 1;
}

// Like the set version, this heap-independent function may have typed but
// unallocated arguments in shared axioms. Its finite key image needs support.
ghost function ReferenceMap(xs: iset<object?>): map<object?, int> {
  map x: object? | x in xs :: 0
}

lemma ReferenceMapMembers(xs: iset<object?>) {
  var m := ReferenceMap(xs);
  assert forall o: object? :: o in m <==> o in xs;
  assert forall o: object? | o in xs :: m[o] == 0;
}

class AllocationCell82 {
  var count: nat
  constructor(n: nat)
    ensures count == n
  {
    count := n;
  }
}

ghost function NonnegativeRange82(n: int): set<int>
  requires 0 <= n
{
  set i: int | 0 <= i < n
}

method LambdaUsesAllocatedFormal() {
  // Source WF checks this child call for allocated lambda arguments; the nat
  // field's validity is available in their heap. Common facts use that boundary.
  ghost var f := (q: AllocationCell82) reads q => NonnegativeRange82(q.count);
  var c := new AllocationCell82(1);
  assert c in f.reads(c);
  assert f(c) == {0};
}
