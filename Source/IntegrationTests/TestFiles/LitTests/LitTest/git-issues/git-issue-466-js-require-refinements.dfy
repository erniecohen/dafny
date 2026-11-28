// RUN: %verify --cores 2 --type-system-refresh --member-invariants --recursive-modules --allow-external-contracts "%s" > "%t-verify"
// RUN: python3 "%repositoryRoot/.github/review/recursive-module-js-require-refinements.py" "%binaryDir/Dafny.dll" "%review-z3" "%S/Inputs" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"

module Cycle {
  import Native
  function Count(n: nat): nat { if n == 0 then 0 else 1 + Native.Count(n - 1) }
}
module {:extern "NativeOwner", "./NativeOwner.js"} Native {
  import Cycle
  function Count(n: nat): nat { if n == 0 then 0 else 1 + Cycle.Count(n - 1) }
  class {:extern} Cell {
    constructor {:extern} (n: int) ensures Read() == n
    function {:extern} Read(): int
  }
}
module {:extern "RefiningOwner", "./RefiningOwner.js"} Refined refines Native { }
abstract module Intermediate refines Native { }
module {:extern "FinalOwner", "./FinalOwner.js"} Final refines Intermediate { }

method Positive() {
  var direct := new Refined.Cell(5);
  var descendant := new Final.Cell(7);
  assert direct.Read() == 5 && descendant.Read() == 7;
}
