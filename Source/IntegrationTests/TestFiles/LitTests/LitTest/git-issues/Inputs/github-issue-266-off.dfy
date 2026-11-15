// Without --modular-termination, "bounded" is an ordinary name and the system module does not declare it.
method M(c: bounded<nat, int -> int>) // error: bounded is not declared
module Mine {
  type bounded = int
  method M(x: bounded) returns (y: int) { y := x; }
}
