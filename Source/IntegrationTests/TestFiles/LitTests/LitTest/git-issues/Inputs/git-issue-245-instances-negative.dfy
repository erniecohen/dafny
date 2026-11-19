include "git-issue-245-instance-orders.dfy"
import opened Std.Termination.Orders
import opened InstanceOrders

function LexSame(s: seq<nat>): nat
  requires |s| <= 2
  decreases s by Lex()
{ LexSame(s) } // error: no strict step

function PointwiseSame(s: seq<nat>): nat
  decreases s by Pointwise()
{ PointwiseSame(s) } // error

function ProductSame(p: (nat, nat)): nat
  decreases p by Product()
{ ProductSame(p) } // error

function InverseSame(p: (nat, nat)): nat
  decreases p by PulledBack()
{ InverseSame((p.0, p.1 + 1)) } // error: changing the ignored field is not descent

// Constructing each order and using a genuine edge must not make false provable.
lemma LexFalse() { assert Lex().gt([2], [1, 100]); assert false; }
lemma PointwiseFalse() { assert Pointwise().gt([2, 3], [2, 2]); assert false; }
lemma ProductFalse() { assert Product().gt((2, 3), (2, 2)); assert false; }
lemma InverseFalse() { assert PulledBack().gt((2, 0), (1, 100)); assert false; }

// Neither an arbitrary relation nor an arbitrary rank supplies a missing well-foundedness premise.
ghost predicate Bad(x: nat, y: nat) { true }
ghost function MissingLex(): Wfo<seq<nat>> { LexicographicOf(Bad, 2) } // error
ghost function MissingPointwise(): Wfo<seq<nat>> { PointwiseOf(Bad) } // error
ghost function MissingProduct(): Wfo<(nat, nat)> { DownWellFounded(); ProductOf(Down, Bad) } // error
ghost function MissingInverse(): Wfo<(nat, nat)> { InverseImageOf(Bad, Rank) } // error
