// Recursive groups can use a renamed refinement and opened copies of a lower module.
// RUN: %verify --allow-warnings --member-invariants --recursive-modules --copy-imports --refinement-renaming "%s" > "%t"
// RUN: %exits-with 4 %verify --allow-warnings --member-invariants --recursive-modules --copy-imports --refinement-renaming "%S/Inputs/git-issue-466-refinement-imports-false.dfy" >> "%t"
// RUN: %resolve --member-invariants --recursive-modules --copy-imports "%S/Inputs/git-issue-466-copy-group.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Original {
  function Id(x: int): (r: int) { x }
  trait {:termination false} Cell extends object { }
  export Public reveals Id, Cell
}
module Renamed refines Original {
  function Id(value: int): (result: int)
}
module Left {
  import Right
  import opened Copy := Renamed`Public
  trait LocalCell extends Cell { }
  function Read(c: Right.Box): int { Id(x := c.value) }
  lemma Good(c: Right.Box)
    ensures Read(c) == c.value
  { }
  method Upcast(c: LocalCell?) returns (r: Cell?) { r := c; }
}
module Right {
  import Left
  datatype Box = Box(value: int)
  function Read(c: Box): int { Left.Read(c) }
  lemma Good(c: Box)
    ensures Read(c) == c.value
  { }
}
