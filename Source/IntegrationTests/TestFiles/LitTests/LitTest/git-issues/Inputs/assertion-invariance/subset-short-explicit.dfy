datatype T = T(i: int)
ghost predicate F(t: T) decreases 1 { G(t) }
ghost predicate G(t: T) decreases 0 { true || F(t) }
type Subset = t: T | F(t) witness T(0)
ghost function Example(i: int): Subset { var t := T(i); assert F(t); t }

