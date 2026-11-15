predicate P(a: int) { a == 7 }
method Main() {
  assert P(7);
  var a :| 0 <= a <= 10 && P(a);
  assert a == 7;
  print a, "\n";
}
