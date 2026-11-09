// Every argument is still checked against the chosen type argument.
datatype List<T> = Nil | Cons(hd: T, tl: List<T>)
datatype CoList<+T> = CoNil | CoCons(hd: T, tl: CoList<T>)
datatype Tree<T> = Leaf | Node(l: Tree<T>, v: T, r: Tree<T>)
type Pos = x: int | x > 0 witness 1

function Unchecked(h: int, ts: List<Pos>): List<Pos> {
  Cons(h, ts)  // error: h may not be Pos
}
method Narrowing(h: int, ts: List<int>) {
  var r := Cons(h, ts);
  var p: List<Pos> := r;  // error: r may hold non-positive elements
}
method Vacuity(h: int, ts: List<Pos>) requires h > 0 {
  var r := Cons(h, ts);
  assert false;  // error
}
method Covariant(h: int, ts: CoList<Pos>) {
  var r: CoList<int> := CoCons(h, ts);  // a covariant parameter is not pinned; verifies
}
method Disagree(l: Tree<Pos>, v: int, r: Tree<int>) {
  var t := Node(l, v, r);  // the two non-variant arguments disagree; error as without the option
}
