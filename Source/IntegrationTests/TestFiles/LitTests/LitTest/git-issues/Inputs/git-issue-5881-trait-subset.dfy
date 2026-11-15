type Unique<T(!new)> = t:T | true witness *
trait Ops { function value(): int }
datatype Op extends Ops = Op { function value():int { 4 } }
datatype Box<O(!new) extends Ops> = Box(x:Unique<O>) { const o := x as Ops }
method Main() { var b: Box<Op> := Box(Op); print b.o.value(), "\n"; }
