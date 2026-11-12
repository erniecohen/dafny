datatype D = D { function {:operator "+"} plus(b: D): int { 0 } } function bad(d: D): D { d.plus(d) }
