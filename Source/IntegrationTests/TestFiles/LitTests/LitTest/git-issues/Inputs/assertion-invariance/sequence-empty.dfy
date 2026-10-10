type S = x:int | x>=0 witness 0
ghost function G(i:int):int { -1 }
ghost function F():seq<S> { seq(0, G) }
