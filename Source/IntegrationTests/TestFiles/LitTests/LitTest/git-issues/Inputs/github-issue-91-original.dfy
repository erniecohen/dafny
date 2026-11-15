module A {
    type O { predicate i() }
}
module B refines A {
    type T { predicate i() }
    type O = T
}
