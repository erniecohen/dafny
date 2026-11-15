// Synonyms without a cycle, which the resolver still expands.  A generic
// synonym may occur more than once in an expansion without a cycle.
type Id<X> = X
type T = Id<Id<int>>
type Nat1 = x: int | 0 <= x
type Nat2 = y: Nat1 | true
type ListAlias = List
datatype List = Nil | Cons(head: T, tail: ListAlias)
datatype D = D(t: T, n: Nat2, l: Id<List>)
codatatype Stream = More(n: Nat2, rest: Id<Stream>)
