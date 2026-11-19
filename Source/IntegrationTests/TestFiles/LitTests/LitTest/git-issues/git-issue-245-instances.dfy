// RUN: %verify --verify-included-files --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders --solver-path %review-z3 "%s" > "%t"
// RUN: %exits-with 0 %verify --verify-included-files --standard-libraries:false --decreases-orders --modular-termination --solver-path %review-z3 "%S/Inputs/git-issue-245-instances-mt.dfy" >> "%t"
// RUN: %exits-with 4 %verify --standard-libraries --decreases-orders --solver-path %review-z3 "%S/Inputs/git-issue-245-instances-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --standard-libraries:false --decreases-orders --modular-termination --solver-path %review-z3 "%S/Inputs/git-issue-245-instances-mt-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

include "Inputs/git-issue-245-instance-orders.dfy"
import opened Std.Termination.Orders
import opened InstanceOrders

// The tail resets, and the length can grow, when the leading component descends.
function LexCount(s: seq<nat>, reset: nat): nat
  requires |s| <= 2
  decreases s by Lex()
{
  if |s| == 0 then 0
  else if |s| == 1 then
    if s[0] == 0 then 1 + LexCount([], reset)
    else 1 + LexCount([s[0] - 1, reset], reset)
  else if s[1] > 0 then
    assert LexicographicGt<nat>(Down, 1, s[1..], [s[1] - 1]);
    assert LexicographicGt<nat>(Down, 2, s, [s[0], s[1] - 1]);
    1 + LexCount([s[0], s[1] - 1], reset)
  else
    assert LexicographicGt<nat>(Down, 1, s[1..], []);
    assert LexicographicGt<nat>(Down, 2, s, s[..1]);
    1 + LexCount(s[..1], reset)
}

function PointwiseCount(s: seq<nat>, i: nat): nat
  requires i < |s|
  decreases s by Pointwise()
{
  if s[i] == 0 then 0 else
    assert PointwiseGt<nat>(Down, s, s[i := s[i] - 1]);
    1 + PointwiseCount(s[i := s[i] - 1], i)
}

function ProductCount(p: (nat, nat)): nat
  decreases p by Product()
{
  if p.0 > 0 then 1 + ProductCount((p.0 - 1, p.1))
  else if p.1 > 0 then 1 + ProductCount((p.0, p.1 - 1))
  else 0
}

function InverseCount(p: (nat, nat)): nat
  decreases p by PulledBack()
{
  if p.0 == 0 then 0 else 1 + InverseCount((p.0 - 1, p.1 + 10))
}

// Positive relation controls include empty bounds, prefixes, tail resets and pointwise incomparability.
lemma Edges() {
  assert Lex().gt([2], [1, 100]);
  assert Lex().gt([2, 0], [2]);
  assert !LexicographicGt<nat>(Down, 0, [], []);
  assert !Lex().gt([2], [1, 0, 0]);
  assert Pointwise().gt([2, 3], [2, 2]);
  assert !Pointwise().gt([2, 3], [1, 4]);
  assert !Pointwise().gt([1], []);
  assert Product().gt((2, 3), (2, 2));
  assert !Product().gt((2, 3), (1, 4));
  assert PulledBack().gt((2, 0), (1, 100));
  assert !PulledBack().gt((2, 0), (2, 1));
}
