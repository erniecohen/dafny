// Input for github-issue-414.dfy: a datatype, forall preconditions and a function-valued parameter,
// around an induction on an integer measure.
datatype Run<S, E> = Run(src: S, dst: S, traces: seq<E>, long: bool, mid: seq<S>, tag: int)
datatype Rel<!S, !E> = Rel(step: (S, E, S) -> bool, init: S -> bool, inv: S -> bool, w: E -> int)

ghost predicate RunOk<S, E>(R: Rel<S, E>, r: Run<S, E>) {
  (r.traces == [] ==> r.dst == r.src && r.mid == []) &&
  |r.mid| == |r.traces| &&
  (forall i :: 0 <= i < |r.traces| ==> R.step(if i == 0 then r.src else r.mid[i - 1], r.traces[i], r.mid[i])) &&
  (forall i :: 0 <= i < |r.mid| ==> R.inv(r.mid[i])) &&
  (r.traces != [] ==> r.dst == r.mid[|r.mid| - 1])
}
ghost function Src<S, E>(r: Run<S, E>): S { r.src }
ghost function Dst<S, E>(r: Run<S, E>): S { r.dst }
ghost predicate Long<S, E>(h: nat -> Run<S, E>, k: nat) { h(k).long || |h(k).traces| > 0 }
ghost predicate Linked<S, E>(h: nat -> Run<S, E>, k: nat) { h(k + 1).src == h(k).dst }
ghost function suc(n: nat): nat { n + 1 }
ghost function P<S, E>(r: Run<S, E>): S { r.src }
ghost function T<S, E>(h: nat -> Run<S, E>): nat -> S { k => h(k).src }
ghost function Q<S>(t: nat -> S, n: nat): S { t(n) }
lemma QSuc<S>(t: nat -> S, n: nat) ensures Q(t, n + 1) == t(n + 1) {}
ghost predicate Ok<S, E>(R: Rel<S, E>, h: nat -> Run<S, E>) {
  (forall k: nat :: RunOk(R, h(k))) &&
  (forall k: nat, i :: 0 <= i < |h(k).traces| ==> R.w(h(k).traces[i]) >= 0) &&
  (forall k: nat :: h(k).tag >= 0) &&
  (forall k: nat :: |h(k).traces| <= h(k).tag) &&
  (forall k: nat :: Linked(h, k)) &&
  (forall k: nat, j: nat :: k <= j ==> h(k).tag <= h(j).tag)
}


lemma Skip<S(!new), E(!new)>(R: Rel<S, E>, h: nat -> Run<S, E>, a: nat, b: nat)
  requires Ok(R, h) && a <= b
  requires forall k: nat :: a <= k < b ==> !Long(h, k)
  ensures P(h(b)) == P(h(a))
  ensures Q(T(h), b) == Q(T(h), a)
  ensures h(a).tag <= h(b).tag
  ensures R.inv(h(a).src) ==> R.inv(h(b).src)
  ensures forall k: nat :: a <= k < b ==> h(k).dst == h(a).src
  decreases b - a
{
  if a < b {
    Skip(R, h, a, b - 1);
    var r := h(b - 1);
    assert RunOk(R, r) && !Long(h, b - 1);
    assert r.traces == [] && Dst(r) == Src(r);
    assert suc(b - 1) == b;
    assert Linked(h, b - 1);
    QSuc(T(h), b - 1);
  }
}
