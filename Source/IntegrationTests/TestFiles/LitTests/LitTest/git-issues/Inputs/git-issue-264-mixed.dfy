datatype D = Leaf | Node(tail: D)
trait T {
  method F(s: seq<D>, d: D)
    requires (s nonincreases to d) && (d nonincreases to s)
    decreases s, 0
}
class C extends T {
  method F(s: seq<D>, d: D)
    decreases d, 1
  {}
}
