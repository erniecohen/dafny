datatype Box<T> = Box(t: T)

function Apply<T>(bx: Box<T>, fn: T --> int): int
  requires fn.requires(bx.t)
{ fn(bx.t) }

method Error() {
  var fn := _ requires true => 0;
  var l := (d: Box<int>) => assert fn.requires(d.t); Apply(d, fn);
}

