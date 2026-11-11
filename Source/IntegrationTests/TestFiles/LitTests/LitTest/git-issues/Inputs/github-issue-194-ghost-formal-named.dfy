datatype D = D {
  function {:operator "+"} plus(ghost b: D): D { this }
}
method Main() { ghost var y := D; var x := (D).plus(y); assert x == D; }
