// Ordinary verified wrappers over the single marked prototype primitive.
include "real-completeness.dfy"

ghost predicate eligible(s: iset<real>) {
  (exists w: real :: w in s) && (exists b: real :: upper(s,b))
}

ghost predicate isLub(s: iset<real>, u: real) {
  upper(s,u) && (forall b: real {:trigger upper(s,b)} :: upper(s,b) ==> u <= b)
}

lemma existsLub(s: iset<real>)
  requires eligible(s)
  ensures exists u: real :: isLub(s,u)
{
  var w: real :| w in s;
  var b: real :| upper(s,b);
  var u := complete(s,w,b);
  assert isLub(s,u);
}

lemma uniqueLub(s: iset<real>, u: real, v: real)
  requires isLub(s,u)
  requires isLub(s,v)
  ensures u == v
{
  assert upper(s,u);
  assert upper(s,v);
}

ghost function sup(s: iset<real>): (u: real)
  ensures eligible(s) ==> isLub(s,u)
  ensures !eligible(s) ==> u == 0.0
{
  if eligible(s) then
    (existsLub(s); var v: real :| isLub(s,v); v)
  else 0.0
}

lemma ivtReturning(c: seq<real>, a: real, b: real) returns (r: real)
  requires a < b
  requires poly(c,a) < 0.0 < poly(c,b)
  ensures a < r < b && poly(c,r) == 0.0
{
  polynomialIvt(c,a,b);
  r :| a < r < b && poly(c,r) == 0.0;
}
