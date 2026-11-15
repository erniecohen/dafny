// Facts about the calls in a heap-free lambda's body, needed where the lambda is
// used in a heap other than the current one or at another type, or applied to a
// formal that is not known to be allocated.  With --heap-free-lambda-facts each method
// verifies; without it, each fails as the finite-support repair (#160) and the
// allocation repair (#137) left it.

ghost function Idx(a: int): int { if 0 <= a < 7 then a else 0 }

// A lambda into an arrow type whose range is a subset type: the type's membership
// quantifies over every good heap, and is checked at int -> nat, not int -> int.
lemma IntoSubsetArrow() returns (rank: int -> nat)
{
  rank := (a: int) => 7 - Idx(a);
}

function IdxC(a: int): int { if 0 <= a < 7 then a else 0 }

method IntoSubsetArrowCompiled() returns (rank: int -> nat)
{
  rank := (a: int) => 7 - IdxC(a);
}

// seq(n, f) into a sequence of a subset type.
type Small = x: int | 0 <= x < 8
function Clamp(x: int): Small { if 0 <= x < 8 then x else 0 }

function Clamped(xs: seq<int>): (ys: seq<Small>)
  ensures |ys| == |xs|
{
  seq(|xs|, i requires 0 <= i < |xs| => Clamp(xs[i]))
}

// An existential over a function, witnessed by a lambda.
type Rel<!S, !E> = (S, seq<E>, S) -> bool

ghost predicate SilentForever<S(!new), E(!new)>(R: Rel<S, E>, s: S)
{
  exists f: nat -> S :: f(0) == s && forall n: nat :: R(f(n), [], f(n + 1))
}

ghost function Path<S>(g: S -> S, s: S, n: nat): S
{
  if n == 0 then s else g(Path(g, s, n - 1))
}

lemma Witness<S(!new), E(!new)>(R: Rel<S, E>, g: S -> S, s: S)
  requires forall x :: R(x, [], g(x))
  ensures SilentForever(R, s)
{
  var f := (n: nat) => Path(g, s, n);
  assert f(0) == s;
  forall n: nat ensures R(f(n), [], f(n + 1)) { }
}

// A family of lambda-built values whose formal is itself a function, under a
// forall in a precondition.
datatype Sem = Sem(step: (int, int, int) -> bool)
datatype Ctx = Ctx(p: int, o: nat -> int)

ghost predicate StepS(x: Ctx, s: int, t: int, s': int) { s' == s + t + x.p }
ghost function Src(x: Ctx): Sem { Sem((s, t, s') => StepS(x, s, t, s')) }
ghost function AtOracle(x: Ctx, o: nat -> int): Ctx { x.(o := o) }
ghost function Fam(x: Ctx): (nat -> int) -> Sem { (o: nat -> int) => Src(AtOracle(x, o)) }

ghost predicate Agrees(p: int, fam: (nat -> int) -> Sem)
{
  forall o: nat -> int, s, t, s' :: fam(o).step(s, t, s') <==> s' == s + t + p
}

lemma FamilyWithArrowFormal(x: Ctx)
  ensures Agrees(x.p, Fam(x))
{
}
