// Tie: two modules whose implementations of an undeclared member call each other through dispatch.
module Tr { trait T { method Go(other: T) } }
module A { import Tr class A extends Tr.T { constructor () { } method Go(other: Tr.T) { other.Go(this); } } }
module B { import Tr class B extends Tr.T { constructor () { } method Go(other: Tr.T) { other.Go(this); } } }
