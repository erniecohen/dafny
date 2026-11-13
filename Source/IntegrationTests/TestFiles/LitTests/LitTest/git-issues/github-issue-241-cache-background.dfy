// RUN: %exits-with 4 %verify --cores:1 --isolate-assertions "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores:1 --isolate-assertions --log-format:csv";"LogFileName="%t.off.csv" "%s" > "%t.off.raw"
// RUN: %exits-with 4 %verify --cores:1 --isolate-assertions --log-format:csv";"LogFileName="%t.on.csv" --boogie /cacheBackground "%s" > "%t.on.raw"
// RUN: %sed 's/^Results File: .*$/Results File: <csv>/' "%t.off.raw" > "%t.off"
// RUN: %sed 's/^Results File: .*$/Results File: <csv>/' "%t.on.raw" > "%t.on"
// RUN: %diff "%t.off" "%t.on"
// RUN: %sed 's/^([^,]*,[^,]*),[^,]*,/$1,/' "%t.off.csv" > "%t.off.unsorted"
// RUN: %sed 's/^([^,]*,[^,]*),[^,]*,/$1,/' "%t.on.csv" > "%t.on.unsorted"
// RUN: sort "%t.off.unsorted" > "%t.off.rows"
// RUN: sort "%t.on.unsorted" > "%t.on.rows"
// RUN: %diff "%t.off.rows" "%t.on.rows"

// Boogie v3.5.5+review.dcd726f6 (issue 241) adds the opt-in /cacheBackground: when a prover
// is set up for a split with the same declarations, seed, type encoding types and SMT options
// as the context it holds, it restarts that context instead of setting it up again, and sends
// the solver the same commands.  With one core and every assertion its own split, the report,
// and the outcome and resource count of every check (the CSV log without its duration column)
// are the same with the option as without it.  The CSV logger orders its rows by duration, so
// the rows are compared sorted.

module A {
  datatype Tree = Leaf | Node(left: Tree, value: int, right: Tree)

  function Size(t: Tree): nat {
    match t
    case Leaf => 0
    case Node(l, _, r) => Size(l) + 1 + Size(r)
  }

  lemma SizeOfNode(l: Tree, v: int, r: Tree)
    ensures Size(Node(l, v, r)) > Size(l)
    ensures Size(Node(l, v, r)) > Size(r)
  {
  }

  lemma SizeIsZero(t: Tree)
    ensures Size(t) == 0  // fails: a node has a positive size
  {
  }
}

module B {
  import opened A

  function Sum(s: seq<int>): int {
    if |s| == 0 then 0 else s[0] + Sum(s[1..])
  }

  method Collect(m: map<int, bool>, xs: set<real>, t: Tree) returns (n: nat)
    ensures n == Size(t)
  {
    var bv: bv8 := 3;
    assert bv + 1 == 4;
    assert Sum([1, 2]) == 3;
    assert 0.5 in xs ==> xs != {};
    assert Sum([1, 2, 3]) == 7;  // fails: the sum is 6
    n := Size(t);
  }

  method Count(s: seq<int>) returns (k: nat)
    ensures k <= |s|
  {
    k := 0;
    var i := 0;
    while i < |s|
      invariant 0 <= i <= |s|
      invariant k <= i
    {
      if s[i] > 0 {
        k := k + 1;
      }
      i := i + 1;
    }
    assert k < |s| || |s| == 0;  // fails: every element may be positive
  }

  lemma Wrong(s: seq<int>)
    ensures Sum(s) >= 0  // fails: s may hold negative numbers
  {
  }
}
