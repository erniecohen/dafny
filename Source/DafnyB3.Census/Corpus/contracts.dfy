method Identity(x: int) returns (y: int) ensures y == x { y := x; }
method BadPost() returns (y: int) ensures y == 1 { y := 0; }
method Positive(x: int) requires x > 0 {}
method BadCall() { Positive(0); }
