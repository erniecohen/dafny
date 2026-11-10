// DESIGN EXPERIMENT ONLY. The single {:axiom} declaration below stands for
// the proposed trusted completeness primitive. It is not a shipped library.

ghost function {:opaque} abs(x: real): real {
  if x < 0.0 then -x else x
}

ghost function min(x: real, y: real): real {
  if x < y then x else y
}

lemma absBasic(x: real)
  ensures 0.0 <= abs(x)
  ensures -abs(x) <= x <= abs(x)
  ensures abs(-x) == abs(x)
  ensures 0.0 <= x ==> abs(x) == x
  ensures x < 0.0 ==> abs(x) == -x
{ reveal abs(); }

lemma absTriangle(x: real, y: real)
  ensures abs(x+y) <= abs(x)+abs(y)
{
  absBasic(x);
  absBasic(y);
  reveal abs();
}

lemma absProduct(x: real, y: real)
  ensures abs(x*y) == abs(x)*abs(y)
{
  reveal abs();
  if x < 0.0 {
    if y < 0.0 {} else {}
  } else {
    if y < 0.0 {} else {}
  }
}

lemma mulLe(x: real, y: real, z: real)
  requires x <= y
  requires 0.0 <= z
  ensures x*z <= y*z
{}

lemma productLe(x: real, y: real, a: real, b: real)
  requires 0.0 <= x <= a
  requires 0.0 <= y <= b
  ensures x*y <= a*b
{
  mulLe(x,a,y);
  mulLe(y,b,a);
}

ghost function {:opaque} poly(c: seq<real>, x: real): real
  decreases |c|
{
  if |c| == 0 then 0.0 else c[0] + x*poly(c[1..],x)
}

ghost function {:opaque} magnitude(c: seq<real>, m: real): real
  decreases |c|
{
  if |c| == 0 then 0.0 else abs(c[0]) + m*magnitude(c[1..],m)
}

ghost function {:opaque} lipschitz(c: seq<real>, m: real): real
  decreases |c|
{
  if |c| == 0 then 0.0 else magnitude(c[1..],m) + m*lipschitz(c[1..],m)
}

lemma polyUnfold(c: seq<real>, x: real)
  ensures poly(c,x) == (if |c| == 0 then 0.0 else c[0]+x*poly(c[1..],x))
{ reveal poly(); }

lemma magnitudeUnfold(c: seq<real>, m: real)
  ensures magnitude(c,m) == (if |c| == 0 then 0.0 else abs(c[0])+m*magnitude(c[1..],m))
{ reveal magnitude(); }

lemma lipschitzUnfold(c: seq<real>, m: real)
  ensures lipschitz(c,m) == (if |c| == 0 then 0.0 else magnitude(c[1..],m)+m*lipschitz(c[1..],m))
{ reveal lipschitz(); }

lemma {:induction false} boundsPositive(c: seq<real>, m: real)
  requires 0.0 <= m
  ensures 0.0 <= magnitude(c,m)
  ensures 0.0 <= lipschitz(c,m)
  decreases |c|
{
  magnitudeUnfold(c,m);
  lipschitzUnfold(c,m);
  if |c| != 0 {
    boundsPositive(c[1..],m);
    absBasic(c[0]);
  }
}

lemma hornerBound(a: real, x: real, q: real, m: real, bound: real)
  requires 0.0 <= m
  requires 0.0 <= bound
  requires abs(x) <= m
  requires abs(q) <= bound
  ensures abs(a+x*q) <= abs(a)+m*bound
{
  absBasic(x);
  absBasic(q);
  absTriangle(a,x*q);
  absProduct(x,q);
  productLe(abs(x),abs(q),m,bound);
}

lemma {:isolate_assertions} hornerLipschitz(a: real, x: real, y: real, qx: real, qy: real,
    m: real, bound: real, lip: real)
  requires 0.0 <= m
  requires 0.0 <= bound
  requires 0.0 <= lip
  requires abs(y) <= m
  requires abs(qx) <= bound
  requires abs(qx-qy) <= lip*abs(x-y)
  ensures abs((a+x*qx)-(a+y*qy)) <= (bound+m*lip)*abs(x-y)
{
  var dx := x-y;
  var dq := qx-qy;
  var r := dx*qx;
  var z := y*dq;
  absBasic(dx);
  absBasic(y);
  absBasic(qx);
  absBasic(dq);
  absProduct(dx,qx);
  absProduct(y,dq);
  absTriangle(r,z);
  mulLe(abs(qx),bound,abs(dx));
  productLe(abs(y),abs(dq),m,lip*abs(dx));
  assert (a+x*qx)-(a+y*qy) == r+z;
  calc {
    abs((a+x*qx)-(a+y*qy));
    == abs(r+z);
    <= abs(r)+abs(z);
    == abs(dx)*abs(qx)+abs(y)*abs(dq);
    <= abs(dx)*bound+m*(lip*abs(dx));
    == (bound+m*lip)*abs(x-y);
  }
}

lemma {:induction false} polynomialBound(c: seq<real>, m: real, x: real)
  requires 0.0 <= m
  requires abs(x) <= m
  ensures abs(poly(c,x)) <= magnitude(c,m)
  decreases |c|
{
  polyUnfold(c,x);
  magnitudeUnfold(c,m);
  absBasic(0.0);
  if |c| != 0 {
    var t := c[1..];
    polynomialBound(t,m,x);
    boundsPositive(t,m);
    var a := c[0];
    var q := poly(t,x);
    var bound := magnitude(t,m);
    hornerBound(a,x,q,m,bound);
    assert abs(a+x*q) <= abs(a)+m*bound;
    assert poly(c,x) == a+x*q;
    assert magnitude(c,m) == abs(a)+m*bound;
  }
}

lemma {:induction false} polynomialLipschitz(c: seq<real>, m: real, x: real, y: real)
  requires 0.0 <= m
  requires abs(x) <= m
  requires abs(y) <= m
  ensures abs(poly(c,x)-poly(c,y)) <= lipschitz(c,m)*abs(x-y)
  decreases |c|
{
  polyUnfold(c,x);
  polyUnfold(c,y);
  lipschitzUnfold(c,m);
  absBasic(0.0);
  if |c| != 0 {
    var t := c[1..];
    polynomialBound(t,m,x);
    polynomialLipschitz(t,m,x,y);
    boundsPositive(t,m);
    var a := c[0];
    var qx := poly(t,x);
    var qy := poly(t,y);
    var bound := magnitude(t,m);
    var lip := lipschitz(t,m);
    hornerLipschitz(a,x,y,qx,qy,m,bound,lip);
    assert abs((a+x*qx)-(a+y*qy)) <= (bound+m*lip)*abs(x-y);
    assert poly(c,x) == a+x*qx;
    assert poly(c,y) == a+y*qy;
    assert lipschitz(c,m) == bound+m*lip;
  }
}

ghost predicate upper(s: iset<real>, b: real) {
  forall x: real {:trigger x in s} :: x in s ==> x <= b
}

// TEMPORARY TRUST BOUNDARY FOR DESIGN BENCHMARKING: the only axiom.
lemma {:axiom} complete(s: iset<real>, w: real, b: real) returns (u: real)
  ensures w in s && upper(s,b) ==> upper(s,u)
  ensures w in s && upper(s,b) ==>
    (forall y: real {:trigger upper(s,y)} :: upper(s,y) ==> u <= y)

lemma lub(s: iset<real>, w: real, b: real) returns (u: real)
  requires w in s
  requires upper(s,b)
  ensures upper(s,u)
  ensures forall y: real {:trigger upper(s,y)} :: upper(s,y) ==> u <= y
{
  u := complete(s,w,b);
}

lemma leastUpperBound(s: iset<real>, w: real, b: real)
  requires w in s
  requires upper(s,b)
  ensures exists u: real :: upper(s,u) && (forall y: real {:trigger upper(s,y)} :: upper(s,y) ==> u <= y)
{
  var u := lub(s,w,b);
}

lemma intervalMagnitude(a: real, b: real, x: real)
  requires a <= x <= b
  ensures abs(x) <= 1.0+abs(a)+abs(b)
{
  absBasic(a);
  absBasic(b);
  absBasic(x);
}

lemma stepBound(k: real, g: real, e: real) returns (d: real)
  requires 0.0 <= k
  requires 0.0 < g
  requires 0.0 < e
  ensures 0.0 < d <= e/2.0
  ensures k*d < g
{
  d := min(e/2.0,g/(2.0*(k+1.0)));
  assert 0.0 < 2.0*(k+1.0);
  assert 0.0 < g/(2.0*(k+1.0));
  assert d <= g/(2.0*(k+1.0));
  assert (2.0*(k+1.0))*(g/(2.0*(k+1.0))) == g;
  assert (2.0*(k+1.0))*d <= g;
}

lemma {:induction false} {:isolate_assertions} polynomialIvt(c: seq<real>, a: real, b: real)
  requires a < b
  requires poly(c,a) < 0.0
  requires 0.0 < poly(c,b)
  ensures exists x: real :: a < x < b && poly(c,x) == 0.0
{
  var m := 1.0+abs(a)+abs(b);
  absBasic(a);
  absBasic(b);
  boundsPositive(c,m);
  var k := lipschitz(c,m);
  var s := iset x: real | a <= x <= b && poly(c,x) <= 0.0;
  assert a in s;
  assert upper(s,b);
  var u := lub(s,a,b);
  assert a <= u <= b;
  intervalMagnitude(a,b,u);
  if poly(c,u) < 0.0 {
    assert u < b;
    var d := stepBound(k,-poly(c,u),b-u);
    var v := u+d;
    assert a <= v <= b;
    intervalMagnitude(a,b,v);
    polynomialLipschitz(c,m,v,u);
    absBasic(poly(c,v)-poly(c,u));
    absBasic(v-u);
    assert abs(v-u) == d;
    assert poly(c,v)-poly(c,u) <= abs(poly(c,v)-poly(c,u));
    assert abs(poly(c,v)-poly(c,u)) <= k*d;
    assert k*d < -poly(c,u);
    assert poly(c,v) < 0.0;
    assert v in s;
    assert v <= u;
    assert false;
  }
  if 0.0 < poly(c,u) {
    assert a < u;
    var d := stepBound(k,poly(c,u),u-a);
    var v := u-d;
    forall x: real | x in s
      ensures x <= v
    {
      assert a <= x <= u <= b;
      if v < x {
        intervalMagnitude(a,b,x);
        polynomialLipschitz(c,m,u,x);
        absBasic(poly(c,u)-poly(c,x));
        absBasic(u-x);
        assert abs(u-x) < d;
        assert k*abs(u-x) <= k*d;
        assert 0.0 < poly(c,x);
        assert false;
      }
    }
    assert upper(s,v);
    assert u <= v;
    assert false;
  }
  assert poly(c,u) == 0.0;
  assert a < u < b;
}

lemma squareRootTwo()
  ensures exists x: real {:nowarn} :: 0.0 < x < 2.0 && x*x == 2.0
{
  var c := [-2.0,0.0,1.0];
  quadraticValue(0.0);
  quadraticValue(2.0);
  assert poly(c,0.0) == -2.0;
  assert poly(c,2.0) == 2.0;
  polynomialIvt(c,0.0,2.0);
  var x: real :| 0.0 < x < 2.0 && poly(c,x) == 0.0;
  quadraticValue(x);
  assert poly(c,x) == -2.0+x*x;
}

lemma quadraticValue(x: real)
  ensures poly([-2.0,0.0,1.0],x) == -2.0+x*x
{
  assert [-2.0,0.0,1.0][1..] == [0.0,1.0];
  assert [0.0,1.0][1..] == [1.0];
  assert [1.0][1..] == [];
  polyUnfold([-2.0,0.0,1.0],x);
  polyUnfold([0.0,1.0],x);
  polyUnfold([1.0],x);
  polyUnfold([],x);
  assert poly([],x) == 0.0;
  assert poly([1.0],x) == 1.0;
  assert poly([0.0,1.0],x) == x;
}
