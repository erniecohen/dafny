// Eta expansion of the function being defined: its termination check fails.  As with every function
// whose termination check fails, the definition is inconsistent with the rank facts, here the new one,
// so Bad verifies, but the program does not (compare github-issue-326-baseline-seq.dfy).
datatype Tree = Leaf | Node(kids: nat -> Tree)

function F(n: nat): Tree { Node((m: nat) => F(m)) }
lemma Bad() ensures false { assert F(0).kids(0) == F(0); assert F(0).kids(0) < F(0); }
