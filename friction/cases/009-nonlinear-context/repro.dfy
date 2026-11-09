function min(x: real, y: real): real { if x < y then x else y }

// x*y lies between a*y and b*y.
lemma Segment(a: real, x: real, b: real, y: real)
  requires a <= x <= b
  ensures min(a*y,b*y) <= x*y
{ if y >= 0.0 { assert a*y <= x*y; } else { assert b*y <= x*y; } }

// Interval multiplication contains every product, from the segment lemma alone.
lemma Inclusion(a: real, b: real, c: real, d: real, x: real, y: real)
  requires a <= x <= b && c <= y <= d
  ensures min(min(a*c,a*d),min(b*c,b*d)) <= x*y
{ Segment(a,x,b,y); Segment(c,y,d,a); Segment(c,y,d,b); }

// The same argument with multiplication behind an opaque function.
opaque function times(x: real, y: real): real { x*y }
lemma SegmentT(a: real, x: real, b: real, y: real)
  requires a <= x <= b
  ensures min(times(a,y),times(b,y)) <= times(x,y) && times(y,a) == times(a,y) && times(y,x) == times(x,y) && times(y,b) == times(b,y)
{ reveal times; if y >= 0.0 { assert a*y <= x*y; } else { assert b*y <= x*y; } }
lemma InclusionT(a: real, b: real, c: real, d: real, x: real, y: real)
  requires a <= x <= b && c <= y <= d
  ensures min(min(times(a,c),times(a,d)),min(times(b,c),times(b,d))) <= times(x,y)
{ SegmentT(a,x,b,y); SegmentT(c,y,d,a); SegmentT(c,y,d,b); }
