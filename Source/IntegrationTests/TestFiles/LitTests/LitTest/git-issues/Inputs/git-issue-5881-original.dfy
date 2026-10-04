type Unique<T(!new)> = t:T | true witness *
trait IntOps<T(!new)> { predicate le(x:T,y:T) }
datatype Int<T(!new),O(!new) extends IntOps<T>> = Int(t:T, o_:Unique<O>) {
  const o := o_ as IntOps<T>
}
