type Kv = nat->nat

// The declared domain must be large enough for every body application.
function Wide(kv: Kv): int->nat { k => kv(k) }

// Even on a nat domain, k - 1 need not be in the callee's domain.
function Shift(kv: Kv): Kv { k => kv(k - 1) }

// Explicit parameter types are checked as written.
function Explicit(kv: Kv): Kv { (k: int) => kv(k) }

// Contextual domain typing does not make a negative result a nat.
function NegativeResult(): Kv { k => -1 }

// Unconstrained lambdas retain base-type inference.
method Unconstrained(kv: Kv) {
  var f := k => kv(k);
}

// Quantifier domains are not narrowed from applications in their bodies.
lemma Quantifier(kv: Kv) {
  assert forall k :: kv(k) == kv(k);
}

// Applying a valid lambda still checks the actual argument.
function Identity(kv: Kv): Kv { k => kv(k) }
lemma BadApplication(kv: Kv) {
  var f := Identity(kv);
  assert f(-1) == 0;
}

// Trigger the repaired declaration before the vacuity control.
lemma NoVacuity(kv: Kv) {
  var f := Identity(kv);
  assert f(0) == kv(0);
  assert false;
}

// Sharing int as a base type does not satisfy another subset's predicate.
type Positive = n: int | 0 < n witness 1
function WrongSubset(kv: Positive->nat): Kv { k => kv(k) }
