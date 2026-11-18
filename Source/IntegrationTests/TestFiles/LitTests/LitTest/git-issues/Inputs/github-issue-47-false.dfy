// A false control: the newly accepted comparisons do not prove false.
trait Number extends object { }
class Integer extends Number { }

method Crossed(i: Integer, j: Integer)
  requires i != j
{
  var a: (Number, Integer) := (i, j);
  var b: (Integer, Number) := (j, i);
  assert a == b;
}

method Same(i: Integer, j: Integer) {
  var a: (Number, Integer) := (i, j);
  var b: (Integer, Number) := (i, j);
  assert a == b;
  assert false;
}
