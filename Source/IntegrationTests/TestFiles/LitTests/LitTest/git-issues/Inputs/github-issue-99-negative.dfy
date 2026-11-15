function Wrong<T(!new)>(x: T): T {
  var id := (y: T) => y;
  forall f: T -> T, a: T | a == x
    ensures f(a) != f(x)
  { }
  id(x)
}

function NestedWrong<T(!new)>(x: T): T {
  var id := (y: T) => y;
  forall f: T -> T, a: T | a == x
    ensures f(a) == f(x)
  {
    forall g: T -> T, a: T {:trigger g(a)} | a == x
      ensures g(a) != g(x)
    { }
  }
  id(x)
}
