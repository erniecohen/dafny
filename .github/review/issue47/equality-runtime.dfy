trait Number extends object { }
class Integer extends Number { }
datatype P<+X, +Y> = Pair(first: X, second: Y)

method Original(i: Integer, j: Integer) returns (r: bool)
  ensures r
{
  var a: (Number, Integer) := (i, j);
  var b: (Integer, Number) := (i, j);
  return a == b;
}

method ExplicitTuples(i: Integer, j: Integer) returns (r: bool)
  ensures r
{
  var n: Number := i;
  var m: Number := j;
  var a: (Number, Integer) := (n, j);
  var b: (Integer, Number) := (i, m);
  return a == b;
}

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

method DirectCommonTuples(i: Integer, j: Integer) returns (r: bool)
  ensures r
{
  var n: Number := i;
  var m: Number := j;
  var a: (Number, Number) := (n, m);
  var b: (Number, Number) := (n, m);
  return a == b;
}

method ExplicitDatatypes(i: Integer, j: Integer) returns (r: bool)
  ensures r
{
  var a := P<Number, Integer>.Pair(i, j);
  var b := P<Integer, Number>.Pair(i, j);
  return a == b;
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
  var original := Original(i, j);
  var tuples := ExplicitTuples(i, j);
  var commonTuples := CommonTupleViews(i, j);
  var directCommon := DirectCommonTuples(i, j);
  var datatypes := ExplicitDatatypes(i, j);
  var commonDatatypes := CommonDatatypeViews(i, j);
  print "original: ", original, "\n";
  print "explicit-tuples: ", tuples, "\n";
  print "common-tuple-views: ", commonTuples, "\n";
  print "direct-common-tuples: ", directCommon, "\n";
  print "explicit-datatypes: ", datatypes, "\n";
  print "common-datatype-views: ", commonDatatypes, "\n";
}
