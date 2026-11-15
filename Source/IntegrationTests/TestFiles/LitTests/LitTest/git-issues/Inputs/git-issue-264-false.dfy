function f(r: real): nat
  requires 0.0 < r <= 1.0
  decreases r, 0
{
  g(r / 2.0) + 1
}

function g(r: real): nat
  requires 0.0 < r <= 1.0
  decreases r, 1, 0
{
  f(r)
}

function Pow2(n: nat): real
  ensures Pow2(n) >= 1.0
{
  if n == 0 then 1.0 else 2.0 * Pow2(n - 1)
}

lemma Grow(n: nat)
  ensures f(1.0) >= n + f(1.0 / Pow2(n))
{
  if n > 0 {
    Grow(n - 1);
    var r := 1.0 / Pow2(n - 1);
    assert r / 2.0 == 1.0 / Pow2(n);
  }
}

lemma False()
  ensures false
{
  Grow(f(1.0) + 1);
}
