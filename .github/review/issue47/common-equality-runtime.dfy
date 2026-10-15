trait Number extends object { }
class Integer extends Number { }
datatype P<+X, +Y> = Pair(first: X, second: Y)

method CommonTupleViews(i: Integer, j: Integer) returns (r: bool)
  ensures r
{
  var n: Number := i;
  var m: Number := j;
  var a: (Number, Integer) := (n, j);
  var b: (Integer, Number) := (i, m);
  var aa: (Number, Number) := a;
  var bb: (Number, Number) := b;
  return aa == bb;
}

method CommonDatatypeViews(i: Integer, j: Integer) returns (r: bool)
  ensures r
{
  var a := P<Number, Integer>.Pair(i, j);
  var b := P<Integer, Number>.Pair(i, j);
  var aa: P<Number, Number> := a;
  var bb: P<Number, Number> := b;
  return aa == bb;
}

method Main() {
  var i := new Integer;
  var j := new Integer;
  var tuples := CommonTupleViews(i, j);
  var datatypes := CommonDatatypeViews(i, j);
  print "common-tuple-views: ", tuples, "\n";
  print "common-datatype-views: ", datatypes, "\n";
}
