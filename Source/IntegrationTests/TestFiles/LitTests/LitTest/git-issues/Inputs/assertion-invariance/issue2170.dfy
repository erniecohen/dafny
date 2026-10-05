datatype Box<T> = Box(t: T)

function Apply<T>(bx: Box<T>, fn: T --> int): int
  requires fn.requires(bx.t)
{ fn(bx.t) }

method Error() {
  var fn := _ requires true => 0;
  var l := (d: Box<int>) => Apply(d, fn); // Error: function precondition might not hold
}

