// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): a class with invariants declares
// no witness (section 3.6 of docs/dev/issue105-member-invariants-plan.md).  Its objects are the ones
// its constructors return, each checked at 'new;', and its possibly-null type has 'null'.

class C {
  const k: int
  invariant k > 0
  witness null                                         // error
  constructor () { k := 1; }
}

class D {
  const k: int
  invariant k > 0
  ghost witness null                                   // error
  constructor () { k := 1; }
}
