// A pattern with a type annotation, inherited by a refining module. The annotation names a type
// parameter of the enclosing class and a type that the refining module defines.
abstract module A {
  datatype Opt<T> = None | Some(v: T)
  type V
  class C<U> {
    method M(d: Opt<U>, e: Opt<V>) {
      match d { case Some(vv: U) => case None => }
      match e { case Some(vv: V) => case None => }
    }
  }
}
module B refines A {
  type V = int
}
