// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Consume<-T> = T -> int witness *
newtype Partial = int --> int witness *
newtype Readful = int ~> int witness *
newtype Zero = f: () -> int | f() == 0 witness (() => 0)

lemma OutsideDeclaredDomain(f: Consume<nat>, x: int) {
  var result := f(x); // ERROR: arbitrary int is not a nat argument
}
lemma CannotBroadenDomain(f: Consume<nat>) {
  var stronger := f as int -> int; // ERROR: totality on all ints is not established
}
lemma CannotAddTotality(f: Partial) {
  var stronger := f as int -> int; // ERROR: a partial function need not be total
}
lemma CannotRemoveReads(f: Readful) {
  var stronger := f as int --> int; // ERROR: reads may be nonempty
}
lemma CannotDropPredicate() {
  var wrong := (() => 1) as Zero; // ERROR: source function fails the nominal predicate
}
