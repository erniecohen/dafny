// Why Std.Axioms.FunctionExtensionality has no generic version over the domain
// (https://github.com/erniecohen/dafny/issues/34), run by ../github-issue-34.dfy with
// --allow-axioms.  Stated for an arbitrary domain A, extensionality proves false: with
// A := nat, two total functions that agree on every nat are equated, but nat -> int and
// int ~> int share a base type, so both are also int ~> int values, which differ at -1.
// This file must keep verifying ("ensures false" is proved); if it ever stops, the
// reason for the restriction should be re-examined, not the restriction dropped.

lemma {:axiom} GenericExt<A(!new), B>(f: A -> B, g: A -> B)
  requires forall x: A :: f(x) == g(x)
  ensures f == g

lemma SubsetDomainProvesFalse()
  ensures false
{
  var f: nat -> int := (x: int) => if x < 0 then 7 else x;
  var g: nat -> int := (x: int) => x;
  assert forall n: nat :: f(n) == n == g(n);
  GenericExt<nat, int>(f, g);
  var f0: int ~> int := f;
  var g0: int ~> int := g;
  assert f0(-1) == 7;
  assert g0(-1) == -1;
}
