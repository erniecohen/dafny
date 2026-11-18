// RUN: %exits-with 4 %verify --relax-definite-assignment --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %audit --member-invariants "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the rows of the vacuous-types
// table (section 5.3 of docs/dev/issue105-member-invariants-plan.md) that the verifier closes, for
// datatypes.  A datatype with invariants and no witness is possibly empty, with a ghost witness it is
// nonempty, and with a compiled witness it is auto-initializable, as a subset type with 'witness *',
// 'ghost witness' or 'witness' is.  No program point may take for granted a value it has not got: a
// local or out-parameter used before it is assigned, a havoc, a such-that, a new array without an
// initializer, or a comprehension over the type.  Each 'assert false' must fail.  Definite assignment
// is relaxed, so that a variable whose type has a witness may be used unassigned, as the table assumes.
// A bodiless or extern function returning such a type is an assumption, as for any type with a
// constraint, and 'dafny audit' reports it.  git-issue-105-emptiness-errors.dfy has the rows that
// resolution closes, and git-issue-105-codatatypes.dfy the co-recursive one.

datatype NoWitness = NW(n: int) {
  invariant n > 0
}
datatype Ghostly<X> = G(n: int) {
  invariant n > 0
  ghost witness G(1)
}
datatype Compiled<X> = C(n: int) {
  invariant n > 0
  witness C(1)
}
// Without invariants, a Holder<X> is as inhabited as an X.
datatype Holder<X> = Holder(x: X)

// Locals and out-parameters.
method Locals() {
  ghost var a: NoWitness;
  assert a.n > 0;  // error: a might be uninitialized
  ghost var b: Ghostly<int>;
  assert b.n > 0;
  var c: Ghostly<int>;
  print c, "\n";  // error: c might be uninitialized
  var d: Compiled<real>;
  assert d.n > 0;
  print d, "\n";
  var e: Holder<Compiled<int>>;
  assert e.x.n > 0;
  ghost var f: Holder<NoWitness>;
  assert f.x.n > 0;  // error: f might be uninitialized
  assert false;  // error
}
method Out() returns (a: NoWitness) {
}  // error: a might be uninitialized
method OutWithWitness() returns (ghost b: Ghostly<int>, c: Compiled<int>) {
  assert b.n > 0 && c.n > 0;
}

// A havoc assigns a value only where the type is nonempty.
method Havoc() {
  ghost var a: NoWitness := *;
  assert a.n > 0;  // error: a might be uninitialized
  ghost var b: Ghostly<int> := *;
  assert b.n > 0;
  assert false;  // error
}

// A such-that must prove that a value exists.
method SuchThat() {
  ghost var a: NoWitness :| true;  // error: cannot establish the existence of a value
  assert false;  // error
}
ghost function Pick(): NoWitness {
  var a: NoWitness :| true; a  // error: cannot establish the existence of a value
}
lemma UsePick() ensures false {  // error
  var a := Pick();
}

// A new array without an initializer needs a compiled default value.
method ArrayNoWitness() {
  var a := new NoWitness[1];  // error
}
method ArrayGhostly() {
  var b := new Ghostly<int>[1];  // error
}
method ArrayCompiled() {
  var c := new Compiled<int>[1];
  assert c[0].n > 0;
  var d := new NoWitness[0];
  assert false;  // error
}

// A comprehension over the type ranges over the values it has.
ghost predicate Positive(a: NoWitness) { a.n > 0 }
lemma Comprehension(s: set<NoWitness>) {
  var t := set a: NoWitness | a in s && Positive(a);
  assert t == s;
  assert false;  // error
}

// Assumptions, which 'dafny audit' reports.
function Arbitrary(): NoWitness
function {:extern} External(): Holder<NoWitness>
// The object a bodiless constructor returns is assumed to satisfy the class's invariants (a class
// that declares no constructor is rejected: git-issue-105-class-errors.dfy).
class Bodiless {
  const k: int
  invariant k > 0
  constructor ()
}
method UseBodiless() returns (b: Bodiless) ensures b.k > 0 {
  b := new Bodiless();
}
