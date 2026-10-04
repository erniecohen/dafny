// The references in a function's result are allocated where the function is applied.
class O { const f: nat }
class C { var s: set<O> }
datatype D = D(o: O)

function F(): set<O>
function G(n: int): set<O>
function S(): seq<O>
function Dt(): D
function M(): map<int, O>
function Generic<T>(x: T): set<T>
function Reads(c: C): set<O> reads c { c.s }
function Body(n: int): set<O> { if n <= 0 then {} else F() + Body(n - 1) }

method Results(n: int, c: C) {
  assert forall o | o in G(n) :: allocated(o);
  assert forall i | 0 <= i < |S()| :: allocated(S()[i]);
  assert allocated(Dt().o);
  assert forall k | k in M() :: allocated(M()[k]);
  assert forall o | o in Generic(c) :: allocated(o);
  assert forall o | o in Reads(c) :: allocated(o);
  assert forall o | o in Body(n) :: allocated(o);
}

// A quantifier over the members of a collection ranges over references the translation takes
// to be allocated; with the option, the verifier knows them to be allocated in every state.
method AfterAllocation(n: int) {
  var x := new O;
  assert forall o | o in G(n) :: allocated(o) && old(allocated(o));
}
