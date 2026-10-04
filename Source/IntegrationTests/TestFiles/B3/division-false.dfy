method ArithmeticDoesNotConcealFalse(x: int, y: int)
  requires y != 0
{
  var quotient := x / y;
  var remainder := x % y;
  assert false;
}
