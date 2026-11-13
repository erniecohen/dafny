datatype D = D {
  ghost function {:operator "+"} plus(b: D): D { b }
}
method Main() { var x := D + D; print x; }
