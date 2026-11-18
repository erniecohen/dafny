// RUN: %run --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %run --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --target py "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of newtypes, compiled.  "raw N"
// is ghost, so a compiled value of N satisfies its invariants, and N is represented as its native type
// where the header constraint and the invariants bound it (Small, Member).  A newtype with a compiled
// witness has it as its default value, for a local variable, an array element or a datatype field,
// and a newtype based on another (Over) keeps the representation of its base.

newtype Even = x: int | true {
  invariant IsEven()
  witness 0
  ghost predicate IsEven() { (this as int) % 2 == 0 }
  function Half(): int { (this as int) / 2 }
  function Next(): Even { this + 2 }
}

newtype Small = x: int | 0 <= x < 100 {
  invariant this as int != 0
  witness 1
}

newtype Member = int {
  invariant 0 <= this as int < 256
  invariant this as int != 0
  witness 3
}

newtype Over = x: Small | true witness 2

newtype Plain = x: int | 0 <= x < 256

datatype D = D(s: Small, m: Member)

method Main() {
  var e: Even := 4;
  print e, " ", e.Half(), " ", e.Next(), "\n";
  var ea := new Even[2];
  var sa := new Small[2];
  var ma := new Member[2];
  var oa := new Over[1];
  var d: D := D(sa[0], ma[1]);
  print ea[0], " ", sa[0], " ", ma[1], " ", oa[0], " ", d, "\n";
  var s: Small := 2;
  s := s + 1;
  var i := s as int + 300;
  print s, " ", i, "\n";
  var p: Plain := 200;
  print p, "\n";
  ghost var r: raw Even := 3 as raw Even;
}
