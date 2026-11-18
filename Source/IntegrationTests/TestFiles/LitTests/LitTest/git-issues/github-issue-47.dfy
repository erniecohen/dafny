// RUN: echo "legacy run" > "%t"
// RUN: %exits-with 0 %baredafny run --type-system-refresh:false --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: echo "refreshed run" >> "%t"
// RUN: %exits-with 0 %baredafny run --type-system-refresh:true --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: echo "negative controls (legacy)" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:false --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-47-negative.dfy" >> "%t"
// RUN: echo "negative controls (refreshed)" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-47-negative.dfy" >> "%t"
// RUN: echo "false control" >> "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh:true --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-47-false.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/47): the refreshed resolver rejected "a == b" for
// a: (Number, Integer) and b: (Integer, Number), where Integer extends Number, because its final check
// required one whole type to be a subtype of the other.  Both have the common supertype (Number, Number),
// and inference already treated types with the same head component by component.  The final check now
// does too: types with the same head are comparable if their arguments are the same at non-variant type
// parameters and comparable at co- and contravariant ones.  This applies to "==", "!=", "in" and prefix
// equality; conversions ("as", "is") are unchanged.  Components with no common supertype, non-variant
// type parameters and different heads are still rejected (Inputs/github-issue-47-negative.dfy), and the
// comparisons do not prove false (Inputs/github-issue-47-false.dfy).

trait Number extends object { }
class Integer extends Number { constructor () { } }
datatype Pair<+A, +B> = Pair(a: A, b: B)

method Tuples(i: Integer, j: Integer) returns (r: bool, s: bool, t: bool)
  ensures r && !s && t
{
  var a: (Number, Integer) := (i, j);
  var b: (Integer, Number) := (i, j);
  r := a == b;
  s := b != a;
  var set_a: set<(Number, Integer)> := {a};
  t := b in set_a;
}

method Nested(i: Integer, j: Integer) returns (r: bool)
  ensures r
{
  var a: ((Number, Integer), Integer) := ((i, j), j);
  var b: ((Integer, Number), Number) := ((i, j), j);
  r := a == b;
}

method Datatypes(i: Integer, j: Integer) returns (r: bool)
  ensures r
{
  var a: Pair<Number, Integer> := Pair(i, j);
  var b: Pair<Integer, Number> := Pair(i, j);
  r := a == b;
}

method Distinct(i: Integer, j: Integer) returns (r: bool)
  requires i != j
  ensures !r
{
  var a: (Number, Integer) := (i, j);
  var b: (Integer, Number) := (j, i);
  r := a == b;
}

method Main() {
  var i := new Integer();
  var j := new Integer();
  var r, s, t := Tuples(i, j);
  var n := Nested(i, j);
  var d := Datatypes(i, j);
  var x := Distinct(i, j);
  print r, " ", s, " ", t, " ", n, " ", d, " ", x, "\n";
}
