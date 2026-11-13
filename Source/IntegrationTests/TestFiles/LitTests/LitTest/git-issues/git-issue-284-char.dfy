// https://github.com/erniecohen/dafny/issues/284
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --unicode-char:false --type-system-refresh:false --consistent-obligation-checks:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --unicode-char:false --type-system-refresh:false --consistent-obligation-checks:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --unicode-char:false --type-system-refresh:true --consistent-obligation-checks:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --unicode-char:false --type-system-refresh:true --consistent-obligation-checks:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --unicode-char:true --type-system-refresh:false --consistent-obligation-checks:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --unicode-char:true --type-system-refresh:false --consistent-obligation-checks:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --unicode-char:true --type-system-refresh:true --consistent-obligation-checks:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --unicode-char:true --type-system-refresh:true --consistent-obligation-checks:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

type AnyChar = c: char | true witness '\0'
datatype Cell = Cell(c: char)

lemma charRange(c: char)
  ensures 0 <= c as int < 0x110000
{}

lemma subsetCharRange(c: AnyChar)
  ensures 0 <= c as int < 0x110000
{}

lemma forallCharRange()
  ensures forall c: char :: 0 <= c as int < 0x110000
{}

lemma fieldCharRange(x: Cell)
  ensures 0 <= x.c as int < 0x110000
{}

lemma charNegative(c: char) {
  assert 0 <= c as int < 0x110000;
  assert false;
}

lemma checkedIntroduction(c: char) {
  var s: AnyChar := c;
  assert s as int == c as int;
}
