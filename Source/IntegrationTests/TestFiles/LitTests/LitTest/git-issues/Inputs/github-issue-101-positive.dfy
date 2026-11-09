// Constructor arguments whose formal type uses the type parameter in a non-variant position.
datatype List<T> = Nil | Cons(hd: T, tl: List<T>)
datatype Snoc<T> = Lin | Snoc(init: Snoc<T>, last: T)  // the non-variant argument comes first
type Pos = x: int | x > 0 witness 1

function Prepend(h: int, ts: List<Pos>): List<Pos> requires h > 0 {
  Cons(h, ts)
}
function Prepend2(a: int, b: int, ts: List<Pos>): List<Pos> requires a > 0 && b > 0 {
  Cons(a, Cons(b, ts))
}
function Append(s: Snoc<Pos>, h: int): Snoc<Pos> requires h > 0 {
  Snoc(s, h)
}
method Local(h: int, ts: List<Pos>) requires h > 0 {
  var r := Cons(h, ts);
  var p: List<Pos> := r;
}

// The program of https://github.com/erniecohen/dafny/issues/101, without the cast.
predicate c(x: T_, y: T_) decreases x.sz() + y.sz() requires x.i() && y.i()
type T = t: T_ | t.i() witness *
datatype T_ = N | P(x: T_, y: T_) {
  function sz(): nat { if N? then 1 else 1 + x.sz() + y.sz() }
  predicate i() decreases sz() { N? || (x.i() && y.i() && c(x, y)) }
}
function lSz(l: List<T>): nat { if l.Nil? then 0 else l.hd.sz() + lSz(l.tl) }
function f(ts: List<T>): List<T> decreases lSz(ts) {
  if ts.Nil? then Nil else if ts.hd.N? then List<T>.Cons(ts.hd, f(ts.tl))
  else Cons(ts.hd.x, Cons(ts.hd.y, ts.tl))
}
