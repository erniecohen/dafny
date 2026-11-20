// RUN: %resolve --type-system-refresh "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// https://github.com/erniecohen/dafny/issues/241
// TypedGraphMaterializerTests captures this program before whole-program admission,
// materializes its module results, and compares with an independent normal resolution.
module Dependency {
  datatype D<T> = D(value: T)
  function Id<T>(d: D<T>): D<T> { d }
  export Public provides D reveals Id
}
module Body {
  import D = Dependency`Public
  function Use<T>(d: D.D<T>): D.D<T> { D.Id(d) }
  function Count(n: nat): nat decreases n {
    if n == 0 then 0 else Count(n - 1)
  }
  function Read(a: array2<int>): int
    requires a.Length0 > 0 && a.Length1 > 0
    reads a
  { a[0, 0] }
  function Update(t: (int, bool)): (int, bool) { t.(0 := 1) }
  lemma Good<T>(d: D.D<T>) ensures Use(d) == d {}
  lemma Bad() { assert false; }
}
