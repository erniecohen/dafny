// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): what 'dafny audit'
// reports for trait axioms.  Each axiom is listed where it is declared, and each type that inherits one
// without overriding it is listed with the axiom's name.

module M {
  type Empty = x: int | false witness *
  trait T {
    ghost function F(): int
    lemma {:axiom} Positive() ensures F() > 0
    ghost function {:axiom} Get(): Empty
    lemma Interface() ensures F() == F()
  }
  class Inherits extends T {
    ghost function F(): int { 0 }
    lemma Interface() ensures F() == F() { }
  }
  class Overrides extends T {
    ghost function F(): int { 1 }
    lemma Positive() ensures F() > 0 { }
    lemma Interface() ensures F() == F() { }
  }
  trait U extends T { }
  class Below extends U {
    ghost function F(): int { 2 }
    lemma Interface() ensures F() == F() { }
  }
}
