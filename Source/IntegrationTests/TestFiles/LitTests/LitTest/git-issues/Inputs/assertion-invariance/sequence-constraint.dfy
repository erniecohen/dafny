type S = x:int | x>=0 witness 0
ghost function G(i:int):int { i }
ghost function F(n:nat):seq<S> { seq(n, G) }
