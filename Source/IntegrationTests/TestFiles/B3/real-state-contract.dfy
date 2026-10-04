method AddOne(x: real) returns (y: real)
  ensures y == x + 1.0
{
  y := x + 1.0;
}
method Test() {
  var y := AddOne(0.25);
  assert y == 1.25;
}
