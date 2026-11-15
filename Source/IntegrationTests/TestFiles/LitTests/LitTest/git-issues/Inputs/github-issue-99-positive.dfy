function Scoped<S(!new), T(!new)>(x: S, y: T): S {
  var r := (o: (S, T)) => o.0;
  forall a, b | a == b ensures r(a) == r(b) { }
  x
}

function Nested<T(!new)>(x: T): T {
  var id := (y: T) => y;
  forall f: T -> T, a: T | a == x
    ensures f(a) == f(x)
  {
    forall g: T -> T, a: T | a == x
      ensures g(a) == g(x)
    { }
  }
  id(x)
}

function Bounded<T(!new)>(x: T): T {
  var id := (y: T) => y;
  forall a: T, b: T {:trigger id(a), id(b)} | a in {x} && b in {a}
    ensures id(a) == id(b)
  { }
  x
}
