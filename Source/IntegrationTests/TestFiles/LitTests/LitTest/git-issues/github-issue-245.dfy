// https://github.com/erniecohen/dafny/issues/245
// With --decreases-orders, a decreases component "e by O" is compared by the well-founded order O,
// a value of the standard library's trait Std.Termination.Orders.Wfo<T>: a call or loop iteration
// must make O.gt(old e, e) true, or leave e unchanged and decrease the components after it.
// RUN: echo "the orders library, verified from its sources" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false "%repositoryRoot/Source/DafnyStandardLibraries/orders/dfyconfig.toml" >> "%t"
// RUN: echo "positive: functions, methods, loops, mixed components, a user implementation of Wfo" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%s" >> "%t"
// RUN: echo "verification errors: wrong direction, another order, an order that is not well-founded, an ill-formed order" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%S/Inputs/github-issue-245-verify.dfy" >> "%t"
// RUN: echo "resolution errors" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%S/Inputs/github-issue-245-resolve.dfy" >> "%t"
// RUN: echo "in a clause of --modular-termination" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders --modular-termination "%S/Inputs/github-issue-245-global.dfy" >> "%t"
// RUN: echo "with --legacy-decreases-order" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders --legacy-decreases-order "%s" >> "%t"
// RUN: echo "without --type-system-refresh and --general-traits=datatype" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries --decreases-orders "%s" >> "%t"
// RUN: echo "without --decreases-orders" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

import opened Std.Termination.Orders

// The order "up to 100" on nat: x is above y when x < y <= 100.
ghost predicate Up(x: nat, y: nat) { x < y <= 100 }

lemma UpWellFounded() ensures wellFounded<nat>(Up) {
  forall c: Chain<nat> ensures stops<nat>(Up, c) {
    if forall n: nat :: Up(c(n), c(n + 1)) {
      Climbs(c, 101);
      assert Up(c(101), c(102));
    }
  }
}

lemma Climbs(c: Chain<nat>, n: nat)
  requires forall k: nat :: Up(c(k), c(k + 1))
  ensures c(n) >= n
{
  if n > 0 { Climbs(c, n - 1); }
}

ghost function UpOrder(): Wfo<nat> { UpWellFounded(); Of(Up) }

function CountUp(i: nat): nat
  requires i <= 100
  decreases i by UpOrder()
{
  if i == 100 then 0 else 1 + CountUp(i + 1)
}

method CountUpM(i: nat) returns (r: nat)
  requires i <= 100
  decreases i by UpOrder()
{
  if i == 100 { return 0; }
  r := CountUpM(i + 1);
  r := r + 1;
}

method Loop() {
  var i: nat := 0;
  while i < 100
    invariant i <= 100
    decreases i by UpOrder()
  {
    i := i + 1;
  }
}

// A component by an order, then an ordinary component: the second decreases while the first stays.
method Nested(i: nat, j: nat)
  requires i <= 100
  decreases i by UpOrder(), j
{
  if j > 0 {
    Nested(i, j - 1);
  } else if i < 100 {
    Nested(i + 1, 10);
  }
}

// Mutual recursion, with the same order in both clauses.
function Even(i: nat): bool
  requires i <= 100
  decreases i by UpOrder()
{
  if i == 100 then true else Odd(i + 1)
}

function Odd(i: nat): bool
  requires i <= 100
  decreases i by UpOrder()
{
  if i == 100 then false else Even(i + 1)
}

// A user implementation of Wfo: the usual order on nat.  Like any datatype that extends a trait of
// another module, it must assume that the calls through the trait terminate.
datatype {:AssumeCrossModuleTermination} Below extends Wfo<nat> = Below {
  ghost predicate gt(x: nat, y: nat) { y < x }
  ghost function aha(c: Chain<nat>): (r: nat) ensures !gt(c(r), c(r + 1)) {
    Descends(c);
    var r: nat :| c(r) <= c(r + 1); r
  }
}

lemma Descends(c: Chain<nat>) ensures exists r: nat :: c(r) <= c(r + 1) {
  if forall k: nat :: c(k + 1) < c(k) {
    Falls(c, c(0) + 1);
  }
}

lemma Falls(c: Chain<nat>, n: nat)
  requires forall k: nat :: c(k + 1) < c(k)
  ensures c(n) + n <= c(0)
{
  if n > 0 { Falls(c, n - 1); }
}

ghost function BelowOrder(): Wfo<nat> { Below }

function CountDown(i: nat): nat
  decreases i by BelowOrder()
{
  if i == 0 then 0 else 1 + CountDown(i - 1)
}
