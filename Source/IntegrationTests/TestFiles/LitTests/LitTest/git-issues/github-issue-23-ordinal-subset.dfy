// RUN: %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// The guarded ORDINAL box inverse preserves membership when converting boxed sets.
lemma Subset(s: set<ORDINAL>, t: set<ORDINAL>)
  requires forall o :: o in s ==> o in t
  ensures s <= t
{ }
