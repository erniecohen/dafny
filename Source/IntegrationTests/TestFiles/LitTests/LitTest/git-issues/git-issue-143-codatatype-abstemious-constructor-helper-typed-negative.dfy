// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function {:abstemious} Carry(s: Outer): Outer { Outer(s, s.field) }
function Bad(): Outer {
  Outer(Outer(Bad(), Carry(Bad()).field), Carry(Bad()).field)
}
lemma Exploit() { var s := Bad(); assert false; }
