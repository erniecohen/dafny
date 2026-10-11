type S = x:int | x>=0 witness 0
ghost function G(i:int):int { i }
ghost function F(n:nat):seq<S> { var s := seq(n, G); assert forall j | 0<=j<|s| :: s[j]>=0; s }
