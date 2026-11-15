type S = x:int | x>=0 witness 0
function G(i:int):int { i }
method F(n:nat) returns (a:array<S>) { assert forall i | 0<=i<n :: G(i)>=0; a := new S[n](G); }
