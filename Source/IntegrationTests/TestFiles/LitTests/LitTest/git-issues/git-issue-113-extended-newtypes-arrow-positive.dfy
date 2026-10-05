// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module ArrowPositive {
  newtype Total = int -> int witness ((x: int) => x)
  newtype Partial = int --> int witness ((x: int) => x)
  newtype General = int ~> int witness ((x: int) => x)
  newtype Nullary = () ~> int witness (() => 0)
  newtype Binary = (int, int) -> int witness ((x: int, y: int) => x + y)
  newtype Endo<!T> = T -> T witness *
  newtype Produce<+T> = () -> T witness *
  newtype Consume<-T> = T -> int witness *
  newtype Nested<!T> = Endo<T> witness *

  newtype Nonnegative = n: int | 0 <= n witness 0
  newtype NonnegativeDomain = Nonnegative -> int
    witness ((x: Nonnegative) => x as int)
  newtype AtZero = f: int -> int | f(0) == 0 witness ((x: int) => x)
  newtype NaturalArrow = int -> nat witness ((x: int) => 0)
  datatype Stored = Stored(value: Total)
  function ReturnTotal(value: Total): Total { value }

  lemma ReturnedAndContainedApplication(f: Total, x: int) {
    var stored := Stored(f);
    assert ReturnTotal(f)(x) == f(x);
    assert stored.value(x) == f(x);
    assert ((((y: int) => y) as Total)(x)) == x;
  }
  newtype Owned = int -> int witness ((x: int) => x) {
    function AtZero(): int { (this as int -> int)(0) }
  }
  newtype OwnedTower = Owned witness (((x: int) => x) as Owned)
  type TotalSubset = f: Total | true witness (((x: int) => x) as Total)
  newtype ThroughSubset = TotalSubset
    witness ((((x: int) => x) as Total) as TotalSubset)

  // Preserving a subset before reaching its nominal Total carrier would stop too
  // early: the selected operation carrier remains the full int -> int base.
  lemma SubsetBetweenNominalLayers(f: ThroughSubset, x: int) {
    assert f(x) == ((f as TotalSubset) as Total)(x);
  }

  // Member lookup selects Owned's nominal receiver, rather than its arrow base.
  lemma IntermediateNominalMember(f: OwnedTower) {
    assert f.AtZero() == (f as Owned).AtZero();
  }

  // Full base refinement is int -> nat, rather than the pre-type int ~> int.
  ghost function RefinedNaturalResult(f: NaturalArrow, x: int): nat {
    f(x)
  }

  ghost function RefinedGenericResult(f: Produce<nat>): nat {
    f()
  }

  lemma TotalRoundTrip(f: int -> int, x: int)
    ensures ((f as Total) as int -> int)(x) == f(x)
    ensures (f as Total)(x) == f(x)
  {
  }

  lemma PartialRoundTrip(f: int --> int, x: int)
    requires f.requires(x)
    ensures (f as Partial).requires(x)
    ensures (f as Partial)(x) == f(x)
  {
  }

  ghost function GeneralApply(f: int ~> int, x: int): int
    requires f.requires(x)
    reads f.reads(x)
  {
    var wrapped := f as General;
    wrapped(x)
  }

  lemma GeneralFramePreserved(f: int ~> int, x: int)
    requires f.requires(x)
    ensures (f as General).requires(x)
    ensures (f as General).reads(x) == f.reads(x)
  {
  }

  lemma GenericResult(f: Produce<nat>)
    ensures 0 <= f()
  {
    var result: nat := f();
  }



  lemma GenericNested(f: int -> int, x: int)
  {
    var once := f as Endo<int>;
    var twice := once as Nested<int>;
    var result: int := twice(x);
    assert result == f(x);
  }

  lemma ConcretePredicate()
  {
    var good := ((x: int) => x) as AtZero;
    assert good(0) == 0;
  }

  class Cell {
    var value: int
    constructor(value: int)
      ensures this.value == value
    {
      this.value := value;
    }
  }

  ghost function ReadCapturedCell(cell: Cell): int
    reads cell
  {
    var f := () reads cell => cell.value;
    var wrapped := f as Nullary;
    wrapped()
  }

  twostate lemma CaptureOldCell(cell: Cell)
  {
    var f: () ~> int := () reads cell => old(cell.value);
    var wrapped := f as Nullary;
    assert wrapped() == old(cell.value);
  }
}
