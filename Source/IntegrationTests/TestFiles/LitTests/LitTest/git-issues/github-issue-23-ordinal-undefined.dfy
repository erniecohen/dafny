// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Well-formedness rejects 0 - 1; its false postcondition must fail independently too.
lemma Undefined()
  ensures ((0 as ORDINAL) - 1) + 1 == 0
{ }
