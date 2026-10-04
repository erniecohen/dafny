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
  assert image == {10, 11};
  assert BooleanImage() == {false, true};
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
  assert seqImage == {0, 1};
  var multiImage := set i: int | i in multiset{0, 1, 1} :: i % 2;
  assert multiImage == {0, 1};
  var keyImage := set i: int | i in map[0 := 5, 1 := 6] :: i + 10;
  assert keyImage == {10, 11};
  var power := set t: set<int> | t <= {0, 1};
  assert power == {{}, {0}, {1}, {0, 1}};
}

lemma DependentAndNestedBounds() {
  var pairs := set i: int, j: int | 0 <= i < 2 && i <= j < 2 :: (i, j);
  assert pairs == {(0, 0), (0, 1), (1, 1)};
  var reversedOrder := set j: int, i: int | 0 <= i < 2 && i <= j < 2 :: (i, j);
  assert reversedOrder == pairs;
  var nested := set i: int | 0 <= i < 2 :: (set j: int | 0 <= j < i + 1);
  assert nested == {{0}, {0, 1}};
  var infiniteOuter := iset i: int | true :: (set j: int | j == i);
  assert {0} in infiniteOuter;
  assert {1} in infiniteOuter;
}

lemma FiniteDomainWithInfiniteWitnesses() {
  var m := map i: int | true :: i == 0 := 0;
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
