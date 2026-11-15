type S = x:int | x>=0 witness 0
function G(i:int):int { i }
method F(n:nat) returns (a:array<S>) { a := new S[n](G); }
