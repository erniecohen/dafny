// A constant whose value refers to itself: resolution rejects it.
datatype Tree = Leaf | Node(kids: nat -> Tree)

const K: nat -> Tree := (n: nat) => Node(K)
lemma Bad() ensures false { assert K(0) == Node(K); assert K(0) < Node(K); }
