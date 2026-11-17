// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), first stage:
// with --recursive-modules, the modules of a cycle of plain imports form a recursive module group.
// --show-hints lists the members of each group at each member.  A group of more than one module is
// not yet supported, so each is an error.  Modules that are only above or below a group, and the
// modules of a diamond, are in no group.
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules --show-hints "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// a group of two
module Two1 {
  import Two2
  datatype T = Leaf | Node(u: Two2.U)
}
module Two2 {
  import Two1
  datatype U = U(t: Two1.T)
}

// a group of three, in a ring
module Three1 {
  import Three2
  function F(n: nat): nat { if n == 0 then 0 else Three2.G(n - 1) }
}
module Three2 {
  import Three3
  function G(n: nat): nat { if n == 0 then 0 else Three3.H(n - 1) }
}
module Three3 {
  import Three1
  function H(n: nat): nat { if n == 0 then 0 else Three1.F(n - 1) }
}

// two sibling modules nested in a third, which is not in the group
module Outer {
  module Left {
    import Right
    const c := 0
    const d := Right.e
  }
  module Right {
    import Left
    const e := Left.c
  }
}

// imports of export sets, opened imports and renamed imports
module Exp1 {
  export E provides T
  import Exp2`E
  type T = int
  const c: Exp2.U := 0
}
module Exp2 {
  export E provides U
  import opened Exp3
  type U = int
  const d: V := 0
}
module Exp3 {
  import X = Exp1`E
  type V = int
  const e: X.T := 0
}

// a module above a group and a module below it are not in it
module Above {
  import Mid1
  const a := Mid1.m
}
module Mid1 {
  import Mid2
  import Below
  const m := Below.b
}
module Mid2 {
  import Mid1
  const n := Mid1.m
}
module Below {
  const b := 0
}

// a diamond has no cycle
module DiamondBottom { const d := 0 }
module DiamondLeft { import DiamondBottom  const l := DiamondBottom.d }
module DiamondRight { import DiamondBottom  const r := DiamondBottom.d }
module DiamondTop { import DiamondLeft  import DiamondRight  const t := DiamondLeft.l + DiamondRight.r }
