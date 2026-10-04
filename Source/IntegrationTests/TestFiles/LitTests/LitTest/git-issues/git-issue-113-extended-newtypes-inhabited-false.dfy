// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Ord = ORDINAL
lemma OrdinalNonVacuity() {
  var n := (7 as ORDINAL) as Ord;
  assert (n as ORDINAL) == 7;
  assert n.IsNat && n.Offset == 7;
  assert false;
}
