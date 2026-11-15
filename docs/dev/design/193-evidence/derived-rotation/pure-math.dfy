// Design-only proof obligations. No trusted bridge occurs in this file.
module BitMath {
  opaque ghost function p(n: nat): nat
    ensures 1 <= p(n)
    decreases n
  { if n == 0 then 1 else 2 * p(n - 1) }

  opaque ghost function andNat(n: nat, a: int, b: int): int
    decreases n
  { if n == 0 then 0 else
      (if a % 2 == 1 && b % 2 == 1 then 1 else 0)
        + 2 * andNat(n - 1, a / 2, b / 2) }

  lemma {:induction false} andZero(n: nat, a: int)
    ensures andNat(n, a, 0) == 0
    decreases n
  {
    reveal andNat();
    if n > 0 { andZero(n - 1, a / 2); }
  }

  lemma {:induction false} lowBit(n: nat, a: int)
    requires 0 < n
    ensures andNat(n, a, 1) == a % 2
  {
    reveal andNat();
    andZero(n - 1, a / 2);
    assert 0 <= a % 2 < 2;
  }

  lemma {:induction false} {:isolate_assertions} {:resource_limit 2000000} powerSum(a: nat, b: nat)
    ensures p(a + b) == p(a) * p(b)
    decreases a
  {
    reveal p();
    if a > 0 { powerSum(a - 1, b); }
  }

  lemma pZero()
    ensures p(0) == 1
  { reveal p(); }

  lemma pOne()
    ensures p(0) == 1 && p(1) == 2
  { reveal p(); }

  // Quotient/remainder uniqueness against a positive divisor.
  lemma {:induction false} {:isolate_assertions} quotient(a: int, b: int, q: int, r: int)
    requires 0 < b
    requires a == q * b + r && 0 <= r < b
    ensures a / b == q && a % b == r
  {
    assert a == (a / b) * b + a % b;
    assert 0 <= a % b < b;
    if a / b < q {
      assert a / b <= q - 1;
      assert (a / b) * b <= (q - 1) * b;
    } else if q < a / b {
      assert q + 1 <= a / b;
      assert (q + 1) * b <= (a / b) * b;
    }
  }

  lemma {:induction false} {:isolate_assertions} rotateArithmetic(a: int, p: int, q: int)
    requires 0 < p && 0 < q && 0 <= a < p * q
    ensures (a * p) % (p * q) == (a % q) * p
    ensures 0 <= (a * p) % (p * q) + a / q < p * q
    ensures (a * p) % (p * q) + a / q == (a % q) * p + a / q
  {
    var d, r := a / q, a % q;
    assert a == d * q + r;
    assert 0 <= r < q;
    assert 0 <= d < p;
    upperProduct(0, p, r);
    strictProduct(r, p, q);
    assert 0 <= r * p < q * p;
    product(d, q, r, p);
    assert a * p == d * (p * q) + r * p;
    quotient(a * p, p * q, d, r * p);
    assert r <= q - 1;
    assert d <= p - 1;
    upperProduct(r, p, q - 1);
    assert r * p + d <= (q - 1) * p + (p - 1);
  }

  lemma product(d: int, q: int, r: int, p: int)
    ensures (d * q + r) * p == d * (p * q) + r * p
  {}

  lemma upperProduct(a: int, b: int, c: int)
    requires 0 <= b && a <= c
    ensures a * b <= c * b
  {}

  lemma strictProduct(a: int, b: int, c: int)
    requires 0 < b && a < c
    ensures a * b < c * b
  {}

  lemma {:induction false} legalCount(n: nat, k: nat)
    requires 0 < n && k <= n
    ensures k < n ==> k % n == k
    ensures k == n ==> k % n == 0
  {
    if k < n { quotient(k, n, 0, k); }
    else { quotient(k, n, 1, 0); }
  }

  ghost function rolNat(n: nat, a: int, k: int): int
  {
    if n == 0 then 0 else
      var r := k % n;
      var u := a % p(n);
      (u * p(r)) % p(n) + u / p(n - r)
  }

  ghost function rorNat(n: nat, a: int, k: int): int
  {
    if n == 0 then 0 else
      var r := k % n;
      var u := a % p(n);
      u / p(r) + (u % p(r)) * p(n - r)
  }

  lemma {:induction false} {:isolate_assertions} rotateLeftMeaning(n: nat, a: int, k: nat)
    requires 0 < n && k <= n && 0 <= a < p(n)
    ensures 0 <= (a * p(k)) % p(n) + a / p(n - k) < p(n)
    ensures (a * p(k)) % p(n) + a / p(n - k) == rolNat(n, a, k)
  {
    legalCount(n, k);
    pZero();
    quotient(a, p(n), 0, a);
    powerSum(k, n - k);
    rotateArithmetic(a, p(k), p(n - k));
    assert a % p(n) == a;
    if k < n {
      assert k % n == k;
    } else {
      assert k == n && k % n == 0;
      quotient(a * p(n), p(n), a, 0);
    }
  }

  lemma {:induction false} {:isolate_assertions} rotateRightMeaning(n: nat, a: int, k: nat)
    requires 0 < n && k <= n && 0 <= a < p(n)
    ensures 0 <= a / p(k) + (a * p(n - k)) % p(n) < p(n)
    ensures a / p(k) + (a * p(n - k)) % p(n) == rorNat(n, a, k)
  {
    legalCount(n, k);
    pZero();
    quotient(a, p(n), 0, a);
    powerSum(n - k, k);
    rotateArithmetic(a, p(n - k), p(k));
    assert a % p(n) == a;
    if k < n {
      assert k % n == k;
    } else {
      assert k == n && k % n == 0;
      quotient(a, 1, a, 0);
    }
  }
}
