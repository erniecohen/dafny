// An existential under a universal is a matching loop when its witness matches the universal's trigger
// (https://github.com/erniecohen/dafny/issues/202). Under --detect-witness-matching-loops, loop detection
// treats the variable of a quantifier that is skolemized at each instance, here p, as a new term, so it
// warns, and prefers another trigger where there is one. Without the option nothing changes.
// RUN: echo "default" > "%t"
// RUN: %exits-with 0 %baredafny verify "%s" --show-hints --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:5000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: echo "--detect-witness-matching-loops" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%s" --show-hints --detect-witness-matching-loops --type-system-refresh --general-traits=full --general-newtypes --solver-path "%review-z3" --cores:1 --resource-limit:5000000 --verification-time-limit:0 --boogie /normalizeDeclarationOrder:0 --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: %diff "%s.expect" "%t"

// The reproducer of issue 202: a subset-type invariant whose witness p re-matches the trigger q in below.
datatype Cut = Cut(below: iset<int>) {
  ghost predicate valid() {
    (exists q :: q in below) && forall q | q in below :: exists p :: p in below && q < p
  }
}
type C = c: Cut | c.valid() witness *

lemma Larger(x: C, q: int) returns (p: int)
  requires q in x.below
  ensures p in x.below && q < p
{ p :| p in x.below && q < p; }

lemma Unrelated(x: C, y: C) ensures x.below == y.below ==> x == y {}

// The same loop, through a universal under a negation and through a negated existential.
ghost predicate Unbounded(s: iset<int>) {
  forall q | q in s :: !(forall p :: p in s ==> p <= q)
}
ghost predicate NoMaximum(s: iset<int>) {
  !(exists q :: q in s && forall p :: p in s ==> p <= q)
}

// Another trigger is available, so it is selected instead of the looping one.
ghost function g(x: int): int
ghost predicate Alternative(s: iset<int>) {
  forall q | q in s :: exists p :: p in s && q < p && g(q) < p
}

// Not loops: here p is a universal variable, not a witness, so no warning.
ghost predicate Minimal(s: iset<int>) {
  forall q | q in s :: forall p :: p in s ==> q <= p
}
ghost predicate NoneBelow(s: iset<int>) {
  forall q | q in s :: !(exists p :: p in s && p < q)
}
ghost predicate InRange(s: iset<int>) {
  forall q | q in s && (exists p :: p in s && q < p) :: q < 100
}
