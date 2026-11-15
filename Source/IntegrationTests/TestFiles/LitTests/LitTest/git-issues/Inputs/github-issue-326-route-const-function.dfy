// A constant and a function that refer to each other: resolution rejects the cycle.
datatype Tree = Leaf | Node(kids: nat -> Tree)

const T0: Tree := Node(F)
function F(n: nat): Tree { T0 }
lemma Bad() ensures false { assert T0.kids(0) == T0; assert T0.kids(0) < T0; }
