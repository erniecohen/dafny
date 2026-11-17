// Orders shared by the tests of the multiset order (github-issue-211-multiset.dfy): the usual order on nat,
// a lexicographic order on pairs, and multiset orders over them.  Each proof of well-foundedness binds its
// chains with quantifiers, so that the orders may also be used in bounds of --modular-termination.
module MultisetOrders {
  import opened Std.Termination.Orders

  ghost predicate {:termination_closed} Gt(x: nat, y: nat) { x > y }

  lemma {:termination_closed} GtWellFounded() ensures wellFounded<nat>(Gt) {
    forall c: Chain<nat> ensures stops<nat>(Gt, c) {
      if forall n: nat :: Gt(c(n), c(n + 1)) {
        Descends(c(0) + 1);
      }
    }
  }

  lemma Descends(n: nat)
    ensures forall c: Chain<nat> :: (forall k: nat :: Gt(c(k), c(k + 1))) ==> c(n) + n <= c(0)
  {
    if n > 0 {
      Descends(n - 1);
      forall c: Chain<nat> | forall k: nat :: Gt(c(k), c(k + 1)) ensures c(n) + n <= c(0) {
        assert Gt(c(n - 1), c(n));
      }
    }
  }

  lemma {:termination_closed} GtTransitive() ensures Transitive<nat>(Gt) { }

  // (a, b) is above (a', b') when a > a', or a == a' and b > b'
  ghost predicate {:termination_closed} Lex(x: (nat, nat), y: (nat, nat)) { x.0 > y.0 || (x.0 == y.0 && x.1 > y.1) }

  lemma {:termination_closed} LexWellFounded() ensures wellFounded<(nat, nat)>(Lex) {
    forall c: Chain<(nat, nat)> ensures stops<(nat, nat)>(Lex, c) {
      if forall n: nat :: Lex(c(n), c(n + 1)) {
        LexDescends(c(0).0, c(0).1);
        assert c(0).0 <= c(0).0;
      }
    }
  }

  // No chain descends forever from a pair at or below (a, b)
  lemma LexDescends(a: nat, b: nat)
    ensures forall c: Chain<(nat, nat)>, k: nat ::
      (forall n: nat :: Lex(c(n), c(n + 1))) && c(k).0 <= a && (c(k).0 == a ==> c(k).1 <= b) ==> false
    decreases a, b
  {
    forall c: Chain<(nat, nat)>, k: nat |
      (forall n: nat :: Lex(c(n), c(n + 1))) && c(k).0 <= a && (c(k).0 == a ==> c(k).1 <= b)
      ensures false
    {
      assert Lex(c(k), c(k + 1));
      if c(k + 1).0 < a {
        LexDescends(c(k + 1).0, c(k + 1).1);
      } else {
        LexDescends(a, b - 1);
      }
    }
  }

  // Bags of call permissions, as multisets of their levels
  ghost function {:termination_closed} Bags(): Wfo<multiset<nat>> { GtWellFounded(); MultisetOf(Gt) }

  // Bags of pairs, ordered lexicographically
  ghost function {:termination_closed} PairBags(): Wfo<multiset<(nat, nat)>> { LexWellFounded(); MultisetOf(Lex) }

  // Bags of bags
  ghost function {:termination_closed} BagsOfBags(): Wfo<multiset<multiset<nat>>> {
    GtWellFounded();
    MultisetOf(MultisetRel<nat>(Gt))
  }

  // k copies of x
  function {:termination_closed} Copies<T>(x: T, k: nat): (m: multiset<T>)
    ensures forall y :: y in m ==> y == x
    ensures m[x] == k
  {
    if k == 0 then multiset{} else Copies(x, k - 1) + multiset{x}
  }
}
