// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): a trait axiom is
// ghost, so a class that inherits it compiles and runs as a class that inherits a ghost member with a body.

trait Shape {
  function Sides(): int
  lemma {:axiom} AtLeastThree() ensures Sides() >= 3
  ghost function {:axiom} Bound(): int ensures Sides() <= Bound()
}

class Triangle extends Shape {
  constructor () { }
  function Sides(): int { 3 }
}

class Square extends Shape {
  constructor () { }
  function Sides(): int { 4 }
}

method Describe(s: Shape) {
  s.AtLeastThree();
  var half := 12 / (s.Sides() - 2);  // the axiom rules out a division by zero
  print s.Sides(), " sides, ", half, "\n";
}

method Main() {
  var t := new Triangle();
  var q := new Square();
  Describe(t);
  Describe(q);
}
