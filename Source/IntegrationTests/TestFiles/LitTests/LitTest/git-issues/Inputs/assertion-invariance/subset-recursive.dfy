datatype D = P(i: int) | Q(d: D)
ghost predicate Valid(d: D) decreases d, 1 { Aux(d) }
ghost predicate Aux(d: D) decreases d, 0 { (d.P? ==> d.i >= 0) && (d.Q? ==> Valid(d.d)) }
type S = d: D | Valid(d) witness P(0)
ghost function F(i: nat): S { var d := P(i); d }

