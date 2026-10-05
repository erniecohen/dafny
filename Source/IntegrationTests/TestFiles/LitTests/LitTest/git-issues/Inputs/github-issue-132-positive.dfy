class C {
  var x: int
  constructor () {}
  method Set5() modifies this ensures x == 5 { x := 5; }
  function F(k: int): int reads this { x + k }
  static function S(k: int): int { k }
  twostate function P(): int reads this { old(x) }

  // Function handles are allocated where their receiver is.
  method Handles() {
    var g := this.F;
    assert allocated(g);
    var s := S;
    assert allocated(s);
    var p := this.P;
    assert allocated(p);
  }

  // Lambdas are allocated where what they capture is: free variables, this, function values.
  method Captures(h: int -> int) {
    var f := (k: int) reads this => h(k) + x;
    assert allocated(f);
    var nested := () => f;
    assert allocated(nested);
    var handle := () => this.F;
    assert allocated(handle);
  }
}

twostate lemma TL(f: () ~> int) {}

method Reads(c: C) {
  var f := () reads c => c.x;
  assert allocated(f);
}

// A lambda whose reads frame depends on the heap, at an earlier label: what it captures is
// allocated there.
method HeapDependentFrame(c: C) modifies c {
  label L:
  c.Set5();
  var f := () reads c, (if c.x == 5 then {c} else {}) => 0;
  assert old@L(allocated(f));
}

// A two-state lemma called at a label gets a function value allocated in the label's heap.
method LabeledCall(c: C) modifies c {
  label L:
  c.x := c.x + 1;
  TL@L(() reads c => c.x);
  var f := () reads c => c.x;
  TL@L(f);
}

// Lambdas that use the previous heap or the heap at a label are allocated in that heap and after it.
method OldLambda(c: C) modifies c {
  c.x := c.x + 1;
  var f := () reads c => old(c.x);
  assert allocated(f);
  assert old(allocated(f));
}

method LabelLambda(c: C) modifies c {
  label L:
  c.x := 2;
  var f := () reads c => old@L(c.x);
  assert allocated(f);
  assert old@L(allocated(f));
}

// A function value stays allocated across calls and allocations.
method AcrossCalls(c: C) modifies c {
  var f := () reads c => c.x;
  c.Set5();
  var n := new C();
  assert allocated(f);
  var g := () reads c, n => c.x + n.x;
  c.Set5();
  assert allocated(g);
}

// The results and reads frames of an allocated function value are allocated.
method Consequences(c: C) {
  var n := new C();
  var f := () reads c => n;
  assert allocated(f());
  assert forall o | o in f.reads() :: allocated(o);
}

// A statement in a lambda in a one-state function: no previous heap is captured.
function WithStatement(n: nat): int -> int {
  (k: int) => (assert n >= 0; n + k)
}

method StatementInFunction() {
  var f := WithStatement(3);
  assert allocated(f);
  assert f(1) == 4;
}

// A two-state lemma called in a lambda captures the previous heap.
twostate lemma Nothing(c: C) ensures true {}

method StatementCallsTwoStateLemma(c: C) modifies c {
  c.x := c.x + 1;
  var f := () reads c => (Nothing(c); c.x);
  assert allocated(f);
  assert old(allocated(f));
}

// A partial or total arrow reads nothing; when its arguments and result involve no references, its values
// show none, so a datatype holding one is allocated in every heap, and it may instantiate a (!new) type
// parameter and be quantified over in a predicate.
datatype TBox = TBox(g: () -> int)

method TotalArrowInDatatype() {
  label L:
  var n := new C();
  var b := TBox(() => if n == n then 0 else 1);
  assert old@L(allocated(b));
}

twostate lemma AllocatedNew<T(!new)>(new x: T) ensures old(allocated(x)) {}

method TotalArrowNotNew() {
  label L:
  var n := new C();
  var g := () => if n == n then 0 else 1;
  AllocatedNew@L(g);
}

ghost predicate AllNonNegative(k: int) {
  forall g: int -> int | g(k) >= 0 :: g(k) + 1 > 0
}
