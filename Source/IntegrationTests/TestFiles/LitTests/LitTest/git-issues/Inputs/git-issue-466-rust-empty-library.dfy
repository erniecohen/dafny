include "git-issue-466-stage7-library.dfy"

// Empty arrays need neither a datatype Default implementation nor a witness call.
module EmptyA {
  import EmptyB
  import A
  function Count(n: nat): nat { if n == 0 then 0 else 1 + EmptyB.Count(n - 1) }
  function Dimension(marker: int, n: nat): nat { n } by method {
    print marker;
    return n;
  }
  method Empty<T(0)>(n: nat) returns (length: int)
    requires n == 0
    ensures length == n
  {
    var values := new T[n];
    length := values.Length;
  }
  method Arrays(n: nat)
    requires n == 0
  {
    var ints := new int[n];
    var trees := new A.Tree<int>[n];
    var witnesses := new A.Positive[n];
    var paths := new A.Path[2, n];
    var boxes := new A.Tree<seq<int>>[n, 3, 4];
    var last := new bool[2, 3, n];
    var middle := new int[Dimension(1, 2), Dimension(2, n), Dimension(3, 3)];
    print " ", ints.Length, " ", trees.Length, " ", witnesses.Length, " ",
      paths.Length0, " ", paths.Length1, " ", boxes.Length0, " ",
      boxes.Length1, " ", boxes.Length2, " ", last.Length2, " ",
      middle.Length0, " ", middle.Length1, " ", middle.Length2, "\n";
    var length := Empty<int>(n);
    print length, " ", Count(4), "\n";
  }
}
module EmptyB {
  import EmptyA
  function Count(n: nat): nat { if n == 0 then 0 else 1 + EmptyA.Count(n - 1) }
}
module EmptyRefined refines EmptyA { }
