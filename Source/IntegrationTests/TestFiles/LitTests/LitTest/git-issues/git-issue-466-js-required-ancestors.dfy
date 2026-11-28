// RUN: %verify --cores 2 --type-system-refresh --member-invariants --recursive-modules --allow-external-contracts "%s" > "%t-verify"
// RUN: python3 "%repositoryRoot/.github/review/recursive-module-js-required-ancestors.py" "%binaryDir/Dafny.dll" "%review-z3" "%S/Inputs" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"

module {:extern "AncestorOwner", "./AncestorOwner.js"} Outside {
  trait {:extern} Root extends object { function {:extern} Value(): int }
}
module {:extern} A {
  import B
  import Outside
  trait {:extern} View extends Outside.Root { }
  class Generated extends View {
    constructor () { }
    function Value(): int { 7 }
  }
  function Count(n: nat): nat { if n == 0 then 0 else 1 + B.Count(n - 1) }
}
module B {
  import A
  function Count(n: nat): nat { if n == 0 then 0 else 1 + A.Count(n - 1) }
}
method Positive() {
  var generated := new A.Generated();
  assert generated.Value() == 7;
  var ancestor: Outside.Root := generated;
  assert ancestor == generated;
}
