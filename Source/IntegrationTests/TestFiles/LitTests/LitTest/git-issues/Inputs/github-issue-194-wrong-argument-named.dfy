datatype D = D { function {:operator "+"} plus(b: int): D { this } } function bad(d: D): D { d.plus(d) }
