// Used by ../github-issue-413.dfy.  Locals of a datatype with no values crashed the verifier
// (https://github.com/erniecohen/dafny/issues/413).  Such a local is not assumed to hold a value.

datatype T = T(t: T)
datatype L = Cons(int, L) | Nil

method Unused() {
  var u: T;
}

method Havoc() {
  var u: T := *;
  assert false; // error: "*" gives u no value, since T may have none
}

method GhostHavoc() {
  ghost var g: T := *;
  assert false; // error
}

// Control: "*" gives a local of a type with values one.
method Control() {
  var l: L := *;
  assert l == Nil || l.Cons?;
}
