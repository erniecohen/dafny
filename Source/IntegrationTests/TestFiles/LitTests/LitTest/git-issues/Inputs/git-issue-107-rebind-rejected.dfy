module Q { class C { } }
module Q1 refines Q { }
module Q2 refines Q1 { }
module Sibling refines Q { }
module Other { class C { } }
module M { import X := Q }
module Step refines M { import X := Q1 }

// Every link must refine the immediately preceding copied target.
module Unrelated refines M { import X := Other }
module Backwards refines Step { import X := Q }
module Sideways refines Step { import X := Sibling }

// Only a copy keeps the type distinctness inherited from M.
module Alias refines M { import X = Q1 }
abstract module Abstract refines M { import X : Q1 }
module Literal refines M { module X refines Q1 { } }
module Type refines M { type X = int }

// The inherited opened status cannot change in either direction.
module Opens refines M { import opened X := Q2 }
module OpenM { import opened X := Q }
module Closes refines OpenM { import X := Q2 }

// Even unused named export sets must be kept at the rebinding site.
module E {
  export A provides F
  export B provides F
  function F(): int { 0 }
}
module E1 refines E { }
module EM { import X := E`{A, B} }
module Drops refines EM { import X := E1`A }

// A member that the new target adds to a trait must be implemented by an inherited class.
module A0 { trait Tr extends object { } }
module A1 refines A0 { trait Tr ... { method N() ensures false } }
module AM {
  import X := A0
  class C extends X.Tr { constructor () { } }
}
module AM1 refines AM { import X := A1 }
