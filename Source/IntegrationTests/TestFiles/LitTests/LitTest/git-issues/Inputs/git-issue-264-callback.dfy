function Apply(g: int -> int, r: real): int
  decreases {:global} r, 2
  decreases {:callback g} r, 0
{ g(0) }

function Forward(g: int -> int, r: real): int
  requires 0.0 < r <= 1.0
  decreases {:global} r, 3
  decreases {:callback g} r / 2.0, 1
{ Apply(g, r) } // callback suffix reset is rejected

function Application(g: int -> int, r: real): int
  requires r >= 0.0
  decreases {:global} r, 1
  decreases {:callback g} r / 2.0, 0
{ g(0) } // weak real progress and strict suffix progress are sufficient
