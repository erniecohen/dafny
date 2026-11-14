// A bare function name as the field of its own result: resolution rejects it.
datatype Tree = Leaf | Node(kids: nat -> Tree)

function F(n: nat): Tree { Node(F) }
lemma Bad() ensures false { assert F(0).kids(0) == F(0); assert F(0).kids(0) < F(0); }
