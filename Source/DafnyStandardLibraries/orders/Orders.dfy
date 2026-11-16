// Well-founded orders for decreases clauses (https://github.com/erniecohen/dafny/issues/245).
//
// With --decreases-orders, a component "e by O" of a decreases clause, where O: Wfo<T> and e: T,
// is compared by O.gt: a call or loop iteration must make O.gt(old e, e) true, or leave e unchanged.
// Every implementation of Wfo verifies the postcondition of aha, so the gt of every value of type
// Wfo<T> is well-founded.
//
// This module is a library of its own, DafnyStandardLibraries-orders.doo, loaded with the standard
// library under --decreases-orders, --type-system-refresh and --general-traits=datatype (or full),
// since the datatype WfoOf implements the trait Wfo.  An order is usually made with Of:
//
//   ghost function UpOrder(): Wfo<nat> { UpWellFounded(); Of(Up) }   // UpWellFounded proves wellFounded(Up)
module Std.Termination.Orders {
  // An infinite sequence of values of T
  type {:termination_closed} Chain<!T(!new)> = nat -> T

  trait Wfo<!T(!new)> {
    // gt(x, y) says that x is greater than y
    ghost predicate gt(x: T, y: T)
    // Every chain stops descending somewhere: aha(c) is such a place
    ghost function aha(c: Chain<T>): (r: nat) ensures !gt(c(r), c(r + 1))
    ghost predicate isZ(t: T) { forall t1 :: !gt(t, t1) }
    ghost predicate ge(t0: T, t1: T) { gt(t0, t1) || t0 == t1 }
    ghost predicate le(t0: T, t1: T) { ge(t1, t0) }
  }

  ghost predicate stops<T(!new)>(gt: (T, T) -> bool, c: Chain<T>) { exists n: nat :: !gt(c(n), c(n + 1)) }
  ghost predicate wellFounded<T(!new)>(gt: (T, T) -> bool) { forall c: Chain<T> :: stops(gt, c) }

  lemma wfoWellFounded<T(!new)>(w: Wfo<T>) ensures wellFounded(w.gt) {
    forall c: Chain<T> ensures stops(w.gt, c) { var n := w.aha(c); }
  }

  // gt is r if r is well-founded, and empty otherwise
  datatype {:termination_closed} WfoOf<!T(!new)> extends Wfo<T> = WfoOf(r: (T, T) -> bool) {
    ghost predicate gt(x: T, y: T) { wellFounded(r) && r(x, y) }
    ghost function aha(c: Chain<T>): (n: nat) ensures !gt(c(n), c(n + 1)) {
      if wellFounded(r) then assert stops(r, c); var n: nat :| !r(c(n), c(n + 1)); n else 0
    }
  }

  // The order whose gt is r.  Its postcondition gives gt directly, so a client of the order does not
  // depend on how a call of gt through the trait reaches WfoOf.gt.
  ghost function Of<T(!new)>(r: (T, T) -> bool): (o: Wfo<T>)
    requires wellFounded(r)
    ensures forall x: T, y: T :: o.gt(x, y) == r(x, y)
  {
    var w := WfoOf(r);
    forall x: T, y: T ensures (w as Wfo<T>).gt(x, y) == r(x, y) {
      assert w.gt(x, y) == r(x, y);
    }
    w
  }

  // ---------------------------------------------------------------------------------------------------------
  // The multiset order over a relation r, in the form of Huet and Oppen that VeriFast uses for its bags of
  // call-permission levels (https://github.com/erniecohen/dafny/issues/211).
  //
  // M is at least N when each element of N - M is below some element of M - N, where "-" is multiset
  // difference; M is above N when, in addition, M != N.  So one element may be replaced by finitely many smaller
  // ones.  The order is proved well-founded for every well-founded r, and transitive for every well-founded r
  // that is transitive, so a multiset order is itself a base for another (multisets of multisets).
  //
  // The lemmas below carry, in parentheses, the names of the corresponding lemmas of VeriFast's termination.gh.
  // Those state the order the other way round: M < N there is MultisetGt(r, N, M) here.

  // M is at least N in the multiset order over r
  ghost predicate MultisetGe<T(!new)>(r: (T, T) -> bool, M: multiset<T>, N: multiset<T>) {
    forall x :: x in N - M ==> exists y :: y in M - N && r(y, x)
  }

  // M is above N in the multiset order over r
  ghost predicate MultisetGt<T(!new)>(r: (T, T) -> bool, M: multiset<T>, N: multiset<T>) {
    MultisetGe(r, M, N) && M != N
  }

  // The multiset order over r, as a relation; it is well-founded when r is
  ghost function MultisetRel<T(!new)>(r: (T, T) -> bool): (s: (multiset<T>, multiset<T>) -> bool)
    ensures forall M: multiset<T>, N: multiset<T> :: s(M, N) == MultisetGt(r, M, N)
    ensures wellFounded(r) ==> wellFounded(s)
  {
    var s := (M: multiset<T>, N: multiset<T>) => MultisetGt(r, M, N);
    MultisetWellFounded(r, s);
    s
  }

  // The multiset order over r, as an order
  ghost function MultisetOf<T(!new)>(r: (T, T) -> bool): (o: Wfo<multiset<T>>)
    requires wellFounded(r)
    ensures forall M: multiset<T>, N: multiset<T> :: o.gt(M, N) == MultisetGt(r, M, N)
  {
    Of(MultisetRel(r))
  }

  ghost predicate Transitive<T(!new)>(r: (T, T) -> bool) {
    forall x: T, y: T, z: T :: r(x, y) && r(y, z) ==> r(x, z)
  }

  // A well-founded relation is irreflexive
  lemma WellFoundedIrreflexive<T(!new)>(r: (T, T) -> bool)
    requires wellFounded(r)
    ensures forall x: T :: !r(x, x)
  {
    forall x: T ensures !r(x, x) {
      var c: Chain<T> := (n: nat) => x;
      assert stops(r, c);
      var n: nat :| !r(c(n), c(n + 1));
    }
  }

  // Replacing m by finitely many elements below it goes down (lemma 7 of the plan of issue 211)
  lemma MultisetReplaceLower<T(!new)>(r: (T, T) -> bool, m: T, L: multiset<T>, X: multiset<T>)
    requires wellFounded(r)
    requires forall x :: x in L ==> r(m, x)
    ensures MultisetGt(r, multiset{m} + X, L + X)
  {
    WellFoundedIrreflexive(r);
    var M, N := multiset{m} + X, L + X;
    assert m !in L;
    assert m in M - N by { assert M[m] == 1 + X[m] && N[m] == X[m]; }
    forall x | x in N - M ensures exists y :: y in M - N && r(y, x) {
      assert x in L by { assert N[x] == L[x] + X[x] && M[x] >= X[x]; }
    }
  }

  // Replacing one element by a lower one goes down (bag_lt_cons_lt)
  lemma MultisetReplaceByLower<T(!new)>(r: (T, T) -> bool, y: T, x: T, X: multiset<T>)
    requires wellFounded(r) && r(y, x)
    ensures MultisetGt(r, multiset{y} + X, multiset{x} + X)
  {
    MultisetReplaceLower(r, y, multiset{x}, X);
  }

  // Replacing an element a of M by finitely many elements below it goes down
  lemma MultisetReplaceOne<T(!new)>(r: (T, T) -> bool, M: multiset<T>, a: T, Y: multiset<T>)
    requires wellFounded(r) && a in M
    requires forall y :: y in Y ==> r(a, y)
    ensures MultisetGt(r, M, M - multiset{a} + Y)
  {
    var X := M - multiset{a};
    MultisetReplaceLower(r, a, Y, X);
    assert multiset{a} + X == M;
    assert Y + X == X + Y;
  }

  // Adding an element goes up (bag_lt_xs_cons_xs)
  lemma MultisetAddOne<T(!new)>(r: (T, T) -> bool, x: T, X: multiset<T>)
    ensures MultisetGt(r, multiset{x} + X, X)
  {
    assert (multiset{x} + X)[x] != X[x];
    forall z | z in X - (multiset{x} + X) ensures false { }
  }

  // A sub-multiset is at most the whole
  lemma MultisetSub<T(!new)>(r: (T, T) -> bool, X: multiset<T>, Y: multiset<T>)
    requires X <= Y
    ensures MultisetGe(r, Y, X)
  {
    forall z | z in X - Y ensures false { assert X[z] <= Y[z]; }
  }

  // Adding elements goes up or stays (bag_le_append_r)
  lemma MultisetGeAdd<T(!new)>(r: (T, T) -> bool, X: multiset<T>, Y: multiset<T>)
    ensures MultisetGe(r, X + Y, Y)
  {
    MultisetSub(r, Y, X + Y);
  }

  // Union is monotone (bag_le_bag_le_append_l)
  lemma MultisetGeUnion<T(!new)>(r: (T, T) -> bool, X1: multiset<T>, X2: multiset<T>, Y: multiset<T>)
    requires MultisetGe(r, X2, X1)
    ensures MultisetGe(r, X2 + Y, X1 + Y)
  {
    forall x | x in (X1 + Y) - (X2 + Y) ensures exists y :: y in (X2 + Y) - (X1 + Y) && r(y, x) {
      assert x in X1 - X2;
      var y :| y in X2 - X1 && r(y, x);
      assert y in (X2 + Y) - (X1 + Y);
    }
  }

  // Adding a common element (bag_le_cons_xs_cons_ys)
  lemma MultisetGeCons<T(!new)>(r: (T, T) -> bool, x: T, X: multiset<T>, Y: multiset<T>)
    requires MultisetGe(r, Y, X)
    ensures MultisetGe(r, multiset{x} + Y, multiset{x} + X)
  {
    MultisetGeUnion(r, X, Y, multiset{x});
    assert X + multiset{x} == multiset{x} + X && Y + multiset{x} == multiset{x} + Y;
  }

  // A multiset with an element above every element of t is above t
  lemma MultisetDrop<T(!new)>(r: (T, T) -> bool, a: T, s: multiset<T>, t: multiset<T>)
    requires wellFounded(r) && a in s
    requires forall y :: y in t ==> r(a, y)
    ensures MultisetGt(r, s, t)
  {
    WellFoundedIrreflexive(r);
    assert a !in t;
    assert a in s - t;
  }

  // The order at least is transitive (bag_le_trans)
  lemma MultisetGeTransitive<T(!new)>(r: (T, T) -> bool, A: multiset<T>, B: multiset<T>, C: multiset<T>)
    requires Transitive(r) && wellFounded(r)
    requires MultisetGe(r, A, B) && MultisetGe(r, B, C)
    ensures MultisetGe(r, A, C)
  {
    WellFoundedIrreflexive(r);
    forall x | x in C - A ensures exists y :: y in A - C && r(y, x) {
      // The elements above x that A has more of than B, or B more of than C.  One of them is the witness.
      var S := set z | z in A + B && r(z, x) && (A[z] > B[z] || B[z] > C[z]);
      if B[x] > A[x] {
        assert x in B - A;
        var z :| z in A - B && r(z, x);
        assert z in S;
      } else {
        assert x in C - B;
        var z :| z in B - C && r(z, x);
        assert z in S;
      }
      var y := Maximal(r, S);
      if A[y] > B[y] {
        if B[y] < C[y] {
          assert y in C - B;
          var z :| z in B - C && r(z, y);
          assert z in S;
        }
      } else if A[y] < B[y] {
        assert y in B - A;
        var z :| z in A - B && r(z, y);
        assert z in S;
      }
      assert y in A - C;
    }
  }

  // A nonempty finite set has an element that nothing in it is above
  lemma Maximal<T(!new)>(r: (T, T) -> bool, S: set<T>) returns (m: T)
    requires Transitive(r) && forall x: T :: !r(x, x)
    requires S != {}
    ensures m in S && forall z :: z in S ==> !r(z, m)
    decreases |S|
  {
    var a :| a in S;
    if S == {a} {
      m := a;
    } else {
      var m' := Maximal(r, S - {a});
      if r(a, m') {
        m := a;
        forall z | z in S ensures !r(z, m) {
          if z != a && r(z, a) { assert r(z, m'); }
        }
      } else {
        m := m';
      }
    }
  }

  // Two multisets each at least the other are equal
  lemma MultisetAntisymmetric<T(!new)>(r: (T, T) -> bool, A: multiset<T>, B: multiset<T>)
    requires wellFounded(r)
    requires MultisetGe(r, A, B) && MultisetGe(r, B, A)
    ensures A == B
  {
    if A != B {
      var s := MultisetRel(r);
      assert s(A, B) && s(B, A);
      var c: Chain<multiset<T>> := (n: nat) => if n % 2 == 0 then A else B;
      forall n: nat ensures s(c(n), c(n + 1)) {
        if n % 2 == 0 {
          assert c(n) == A && c(n + 1) == B;
        } else {
          assert c(n) == B && c(n + 1) == A;
        }
      }
      assert stops(s, c);
    }
  }

  // A >= B > C implies A > C
  lemma MultisetGeGt<T(!new)>(r: (T, T) -> bool, A: multiset<T>, B: multiset<T>, C: multiset<T>)
    requires Transitive(r) && wellFounded(r)
    requires MultisetGe(r, A, B) && MultisetGt(r, B, C)
    ensures MultisetGt(r, A, C)
  {
    MultisetGeTransitive(r, A, B, C);
    if A == C {
      MultisetAntisymmetric(r, B, C);
    }
  }

  // A > B >= C implies A > C (bag_le_bag_lt, and rule 10 of the plan of issue 211)
  lemma MultisetGtGe<T(!new)>(r: (T, T) -> bool, A: multiset<T>, B: multiset<T>, C: multiset<T>)
    requires Transitive(r) && wellFounded(r)
    requires MultisetGt(r, A, B) && MultisetGe(r, B, C)
    ensures MultisetGt(r, A, C)
  {
    MultisetGeTransitive(r, A, B, C);
    if A == C {
      MultisetAntisymmetric(r, A, B);
    }
  }

  // The multiset order over a well-founded transitive relation is transitive, so it is itself a base for
  // another multiset order
  lemma MultisetTransitive<T(!new)>(r: (T, T) -> bool)
    requires Transitive(r) && wellFounded(r)
    ensures Transitive(MultisetRel(r))
  {
    var s := MultisetRel(r);
    forall M: multiset<T>, N: multiset<T>, K: multiset<T> | s(M, N) && s(N, K) ensures s(M, K) {
      MultisetGtGe(r, M, N, K);
    }
  }

  // The proof that the multiset order is well-founded.  Each step of the order of Huet and Oppen is a step of the
  // order of Dershowitz and Manna: M becomes N by replacing M - N by N - M.  The proof that the latter is
  // well-founded follows the accessibility proof for the one-step order (Nipkow, Isabelle's Multiset theory),
  // with "every chain from x stops" for "x is accessible".  The predicates are opaque, so that their quantifiers
  // do not feed each other.

  lemma MultisetWellFounded<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == MultisetGt(r, M, N)
    ensures wellFounded(r) ==> wellFounded(s)
  {
    if wellFounded(r) {
      var d := (M: multiset<T>, N: multiset<T>) => DmGt(r, M, N);
      DmWellFounded(r, d);
      forall M: multiset<T>, N: multiset<T> | s(M, N) ensures d(M, N) {
        GtDm(r, M, N);
      }
      SmallerWellFounded(d, s);
    }
  }

  // A relation contained in a well-founded one is well-founded
  lemma SmallerWellFounded<U(!new)>(g: (U, U) -> bool, h: (U, U) -> bool)
    requires wellFounded(g) && forall a: U, b: U :: h(a, b) ==> g(a, b)
    ensures wellFounded(h)
  {
    forall c: Chain<U> ensures stops(h, c) {
      assert stops(g, c);
      var n: nat :| !g(c(n), c(n + 1));
    }
  }

  lemma GtDm<T(!new)>(r: (T, T) -> bool, M: multiset<T>, N: multiset<T>)
    requires MultisetGt(r, M, N)
    ensures DmGt(r, M, N)
  {
    var X, Y := M - N, N - M;
    if X == multiset{} {
      forall z ensures M[z] == N[z] {
        if z in Y {
          var y :| y in X && r(y, z);
        }
        assert X[z] == 0;
      }
      assert M == N;
    }
    assert N == M - X + Y;
  }

  // Every element of Y is below some element of X
  ghost predicate Dominated<T(!new)>(r: (T, T) -> bool, X: multiset<T>, Y: multiset<T>) {
    forall y :: y in Y ==> exists x :: x in X && r(x, y)
  }

  // M is above N in the order of Dershowitz and Manna over r
  ghost predicate DmGt<T(!new)>(r: (T, T) -> bool, M: multiset<T>, N: multiset<T>) {
    exists X: multiset<T>, Y: multiset<T> :: X != multiset{} && X <= M && N == M - X + Y && Dominated(r, X, Y)
  }

  lemma DmReplace<T(!new)>(r: (T, T) -> bool, M: multiset<T>, X: multiset<T>, Y: multiset<T>)
    requires X != multiset{} && X <= M && Dominated(r, X, Y)
    ensures DmGt(r, M, M - X + Y)
  {
    assert X != multiset{} && X <= M && M - X + Y == M - X + Y && Dominated(r, X, Y);
  }

  // Every chain of g that starts at x stops descending
  opaque ghost predicate AccFrom<U(!new)>(g: (U, U) -> bool, x: U) {
    forall c: Chain<U> :: c(0) == x ==> stops(g, c)
  }

  // P holds of z whenever it holds of everything below z
  opaque ghost predicate Progressive<U(!new)>(g: (U, U) -> bool, P: U -> bool) {
    forall z: U :: (forall w: U :: g(z, w) ==> P(w)) ==> P(z)
  }

  // x is accessible if everything below it is
  lemma AccStep<U(!new)>(g: (U, U) -> bool, x: U)
    requires forall y: U :: g(x, y) ==> AccFrom(g, y)
    ensures AccFrom(g, x)
  {
    forall c: Chain<U> | c(0) == x ensures stops(g, c) {
      if g(c(0), c(1)) {
        var d: Chain<U> := (n: nat) => c(n + 1);
        UseAcc(g, c(1), d);
        var n: nat :| !g(d(n), d(n + 1));
        assert !g(c(n + 1), c(n + 1 + 1));
      } else {
        assert !g(c(0), c(0 + 1));
      }
    }
    reveal AccFrom();
  }

  // Everything below an accessible x is accessible
  lemma AccDown<U(!new)>(g: (U, U) -> bool, x: U, y: U)
    requires AccFrom(g, x) && g(x, y)
    ensures AccFrom(g, y)
  {
    reveal AccFrom();
    forall c: Chain<U> | c(0) == y ensures stops(g, c) {
      var d: Chain<U> := (n: nat) => if n == 0 then x else c(n - 1);
      assert d(0) == x;
      assert stops(g, d);
      var k: nat :| !g(d(k), d(k + 1));
      assert k != 0 by { assert d(1) == y; }
      assert !g(c(k - 1), c(k - 1 + 1)) by { assert d(k) == c(k - 1) && d(k + 1) == c(k); }
    }
  }

  // Below a z without P there is a w without P
  lemma Below<U(!new)>(g: (U, U) -> bool, P: U -> bool, z: U)
    requires Progressive(g, P) && !P(z)
    ensures exists w: U :: g(z, w) && !P(w)
  {
    reveal Progressive();
  }

  // A chain that keeps descending below an x without P
  ghost function BadChain<U(!new)>(g: (U, U) -> bool, P: U -> bool, x: U, n: nat): (y: U)
    requires Progressive(g, P) && !P(x)
    ensures !P(y)
  {
    if n == 0 then x else
      var z := BadChain(g, P, x, n - 1);
      Below(g, P, z);
      var w :| g(z, w) && !P(w);
      w
  }

  // Induction along g from an accessible x
  lemma AccInduct<U(!new)>(g: (U, U) -> bool, P: U -> bool, x: U)
    requires AccFrom(g, x) && Progressive(g, P)
    ensures P(x)
  {
    if !P(x) {
      var c: Chain<U> := (n: nat) => BadChain(g, P, x, n);
      forall n: nat ensures g(c(n), c(n + 1)) {
        assert c(n + 1) == BadChain(g, P, x, n + 1);
      }
      UseAcc(g, x, c);
    }
  }

  lemma UseAcc<U(!new)>(g: (U, U) -> bool, x: U, c: Chain<U>)
    requires AccFrom(g, x) && c(0) == x
    ensures stops(g, c)
  {
    reveal AccFrom();
  }

  lemma DmWellFounded<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == DmGt(r, M, N)
    ensures wellFounded(r) ==> wellFounded(s)
  {
    if wellFounded(r) {
      forall c: Chain<multiset<T>> ensures stops(s, c) {
        AllAccessible(r, s, c(0));
        reveal AccFrom();
      }
    }
  }

  lemma AllAccessible<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool, M: multiset<T>)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == DmGt(r, M, N)
    requires wellFounded(r)
    ensures AccFrom(s, M)
    decreases |M|
  {
    if M == multiset{} {
      forall N: multiset<T> ensures !s(M, N) {
        if DmGt(r, M, N) {
          var X: multiset<T>, Y: multiset<T> :| X != multiset{} && X <= M && N == M - X + Y && Dominated(r, X, Y);
          var x :| x in X;
        }
      }
      AccStep(s, M);
    } else {
      var a :| a in M;
      AllAccessible(r, s, M - multiset{a});
      AddAccessible(r, s, a, M - multiset{a});
      assert M - multiset{a} + multiset{a} == M;
    }
  }

  // Adding any element to an accessible multiset keeps it accessible: by induction along r
  lemma AddAccessible<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool, a: T, K: multiset<T>)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == DmGt(r, M, N)
    requires wellFounded(r) && AccFrom(s, K)
    ensures AccFrom(s, K + multiset{a})
  {
    var P := (b: T) => forall K: multiset<T> :: AccFrom(s, K) ==> AccFrom(s, K + multiset{b});
    assert Progressive(r, P) by {
      reveal Progressive();
      forall b: T | forall b': T :: r(b, b') ==> P(b') ensures P(b) {
        forall K: multiset<T> | AccFrom(s, K) ensures AccFrom(s, K + multiset{b}) {
          AddAccessibleTo(r, s, b, K);
        }
      }
    }
    assert AccFrom(r, a) by {
      reveal AccFrom();
      forall c: Chain<T> | c(0) == a ensures stops(r, c) { }
    }
    AccInduct(r, P, a);
  }

  // The step for b: by induction along s from K
  lemma AddAccessibleTo<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool, b: T, K: multiset<T>)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == DmGt(r, M, N)
    requires forall b': T :: r(b, b') ==> forall K: multiset<T> :: AccFrom(s, K) ==> AccFrom(s, K + multiset{b'})
    requires AccFrom(s, K)
    ensures AccFrom(s, K + multiset{b})
  {
    var P := (M: multiset<T>) => AccFrom(s, M) ==> AccFrom(s, M + multiset{b});
    assert Progressive(s, P) by {
      reveal Progressive();
      forall M: multiset<T> | forall M': multiset<T> :: s(M, M') ==> P(M') ensures P(M) {
        if AccFrom(s, M) {
          forall N: multiset<T> | s(M + multiset{b}, N) ensures AccFrom(s, N) {
            AddAccessibleBelow(r, s, b, M, N);
          }
          AccStep(s, M + multiset{b});
        }
      }
    }
    AccInduct(s, P, K);
  }

  // Each N below M + {b} is accessible, given that M' + {b} is for each M' below M
  lemma AddAccessibleBelow<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool, b: T, M: multiset<T>, N: multiset<T>)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == DmGt(r, M, N)
    requires forall b': T :: r(b, b') ==> forall K: multiset<T> :: AccFrom(s, K) ==> AccFrom(s, K + multiset{b'})
    requires forall M': multiset<T> :: s(M, M') ==> (AccFrom(s, M') ==> AccFrom(s, M' + multiset{b}))
    requires AccFrom(s, M) && s(M + multiset{b}, N)
    ensures AccFrom(s, N)
  {
    var X: multiset<T>, Y: multiset<T> :|
      X != multiset{} && X <= M + multiset{b} && N == M + multiset{b} - X + Y && Dominated(r, X, Y);
    if X <= M {
      RemoveKept(M, X, Y, b);
      KeptCase(r, s, b, M, X, Y);
    } else {
      RemoveTaken(M, X, Y, b);
      TakenCase(r, s, b, M, X, Y);
    }
  }

  // N keeps b: it is M' + {b} for an M' below M
  lemma KeptCase<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool, b: T, M: multiset<T>, X: multiset<T>, Y: multiset<T>)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == DmGt(r, M, N)
    requires forall M': multiset<T> :: s(M, M') ==> (AccFrom(s, M') ==> AccFrom(s, M' + multiset{b}))
    requires AccFrom(s, M) && X != multiset{} && X <= M && Dominated(r, X, Y)
    ensures AccFrom(s, M - X + Y + multiset{b})
  {
    DmReplace(r, M, X, Y);
    AccDown(s, M, M - X + Y);
  }

  // N takes b: each element of Y is below b or below an element of the rest of X
  lemma TakenCase<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool, b: T, M: multiset<T>, X: multiset<T>, Y: multiset<T>)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == DmGt(r, M, N)
    requires forall b': T :: r(b, b') ==> forall K: multiset<T> :: AccFrom(s, K) ==> AccFrom(s, K + multiset{b'})
    requires AccFrom(s, M) && X - multiset{b} <= M && Dominated(r, X, Y)
    ensures AccFrom(s, M - (X - multiset{b}) + Y)
  {
    var X' := X - multiset{b};
    forall y | y in Y ensures r(b, y) || exists x :: x in X' && r(x, y) {
      var x :| x in X && r(x, y);
      if x != b {
        assert x in X';
      }
    }
    Replaced(r, s, b, M, X', Y, multiset{});
    assert M - X' + multiset{} + Y == M - X' + Y;
  }

  lemma RemoveKept<T>(M: multiset<T>, X: multiset<T>, Y: multiset<T>, b: T)
    requires X <= M
    ensures M + multiset{b} - X + Y == M - X + Y + multiset{b}
  {
    forall z ensures (M + multiset{b} - X + Y)[z] == (M - X + Y + multiset{b})[z] { }
  }

  lemma RemoveTaken<T>(M: multiset<T>, X: multiset<T>, Y: multiset<T>, b: T)
    requires X <= M + multiset{b} && !(X <= M)
    ensures X - multiset{b} <= M && M + multiset{b} - X + Y == M - (X - multiset{b}) + Y
  {
    var z0 :| X[z0] > M[z0];
    assert z0 == b;
    forall z ensures (X - multiset{b})[z] <= M[z] && (M + multiset{b} - X + Y)[z] == (M - (X - multiset{b}) + Y)[z] { }
  }

  // M - X' + Z + Y is accessible when M is, Z is dominated by X', and each element of Y is below b or below
  // an element of X': by induction on Y
  lemma Replaced<T(!new)>(r: (T, T) -> bool, s: (multiset<T>, multiset<T>) -> bool, b: T,
                          M: multiset<T>, X': multiset<T>, Y: multiset<T>, Z: multiset<T>)
    requires forall M: multiset<T>, N: multiset<T> :: s(M, N) == DmGt(r, M, N)
    requires forall b': T :: r(b, b') ==> forall K: multiset<T> :: AccFrom(s, K) ==> AccFrom(s, K + multiset{b'})
    requires AccFrom(s, M) && X' <= M && Dominated(r, X', Z)
    requires forall y :: y in Y ==> r(b, y) || exists x :: x in X' && r(x, y)
    ensures AccFrom(s, M - X' + Z + Y)
    decreases |Y|
  {
    if Y == multiset{} {
      if X' == multiset{} {
        if Z != multiset{} {
          var z :| z in Z;
        }
        assert M - X' + Z + Y == M;
      } else {
        DmReplace(r, M, X', Z);
        AccDown(s, M, M - X' + Z);
        assert M - X' + Z + Y == M - X' + Z;
      }
    } else {
      var y :| y in Y;
      var Y1 := Y - multiset{y};
      if exists x :: x in X' && r(x, y) {
        Replaced(r, s, b, M, X', Y1, Z + multiset{y});
        assert M - X' + (Z + multiset{y}) + Y1 == M - X' + Z + Y;
      } else {
        Replaced(r, s, b, M, X', Y1, Z);
        assert M - X' + Z + Y1 + multiset{y} == M - X' + Z + Y;
      }
    }
  }
}
