function F(r: real): int
  requires 0.0 < r <= 1.0
  decreases r, 0
{ G(r / 2.0) }

function G(r: real): int
  requires 0.0 < r <= 1.0
  decreases r, 1, 0
{ F(r) }
