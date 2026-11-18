// Member invariants (https://github.com/erniecohen/dafny/issues/105): a client uses "raw L.D" and the
// raw members of a datatype declared in a library, given as source or as a .doo file built with
// --member-invariants. The library's raw members are inferred as when it is compiled on its own.
// RUN: %build -t:lib --no-verify --member-invariants "%S/Inputs/git-issue-105-raw-library.dfy" --output "%t.doo" > "%t"
// RUN: %resolve --member-invariants --print-mode NoIncludes --rprint:- "%s" --library "%t.doo" >> "%t"
// RUN: %resolve --member-invariants "%s" --library "%S/Inputs/git-issue-105-raw-library.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Client {
  import L = RawLib

  method M(i: L.Interval) returns (r: raw L.Interval, ok: bool, w: int, f: L.Interval) {
    r := L.Make(3, 1);
    ok := r.Ok();
    w := i.Width();
    f := L.Fix(r);
    var j: raw L.Interval := L.Interval(0, 0);
    var k: raw L.Interval := i.(lo := 5);
    var s: seq<raw L.Interval> := [i, j, k];
  }
}
