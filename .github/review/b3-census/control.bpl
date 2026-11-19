// {:inside_comment} old free where while assert
function id<T>(x: T): T;
function {:bvbuiltin "bvadd"} add(x: bv8, y: bv8): bv8;
var h: int;
hideable axiom (forall x: int :: { id(x) } id(x) == x);
procedure {:census_control} {:second_control} P(x: int where x >= 0) returns (y: int where y >= 0);
  free requires x >= 0;
  requires x < 100;
  free ensures y >= 0;
  ensures h == old(h);
implementation P(x: int) returns (y: int) {
  var z: int where z >= 0;
  var m: [int]int;
  m := (lambda i: int :: i);
  y := m[x];
  if (x > 0) { y := x; } else { y := 0; }
  while (y > 0) invariant y >= 0; { y := y - 1; }
  havoc z;
  assert {:subsumption 0} y == 0;
}
