// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Each failure below has a separate proof obligation. Capture exact diagnostics before registration.

datatype L = Nil | Cons(head: int, tail: L)
newtype NL = L witness *
datatype R = R(value: int, ghost proof: int)
newtype Positive = r: R | r.value > 0 witness R(1, 0)
newtype ZeroProof = r: R | r.proof == 0 ghost witness R(1, 0)

method BadUpdate(p: Positive) {
  var q: Positive := p.(value := 0); // fails reintroduction into Positive
}

lemma BadGhostUpdate(p: ZeroProof) {
  var q: ZeroProof := p.(proof := 1); // fails even if runtime update erases
}

method BadConstruction() {
  var q := R(0, 0) as Positive; // fails destination predicate
}

method UnguardedDestructor(n: NL) {
  var tail := n.tail; // fails constructor definedness
}

function NoProgress(n: NL): int
  decreases n
{
  NoProgress((n as L) as NL) // cast identity is not structural decrease
}

method DatatypeVacuity(p: Positive) {
  assert p.R?;
  assert p.value > 0;
  assert false; // feature and membership facts must remain non-vacuous
}
