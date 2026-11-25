// RUN: %exits-with 4 %verify --show-snippets=false "%s" > "%t"
// RUN: %exits-with 4 %verify --additional-axioms --show-snippets=false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/572: the reads of f.requires are exactly the
// reads of f.  A frame component that is an actual finite set contributes exactly its own
// members; the canonical-box guard stays on components that select through unboxing.

class Cell { var data: int }

// Positive controls.
lemma RequiresReadsArity1(f: int ~> int)
  requires f.requires(10)
{
  var g := f.requires;
  assert g.reads(10) == f.reads(10);
}

lemma RequiresReadsArity2(f: (int, bool) ~> int)
  requires f.requires(3, true)
{
  var g := f.requires;
  assert g.reads(3, true) == f.reads(3, true);
}

method RequiresReadsOfLambda(c: Cell, d: Cell) {
  var f := (x: int) reads c, d => c.data + d.data + x;
  var g := f.requires;
  assert g.reads(1) == f.reads(1) == {c, d};
}

// Negative controls: the equality says nothing more about f's reads.
lemma NotEmpty(f: int ~> int)
  requires f.requires(10)
{
  var g := f.requires;
  assert g.reads(10) == {};  // error
}

lemma NotMember(f: int ~> int, c: Cell)
  requires f.requires(10)
{
  var g := f.requires;
  assert c in g.reads(10);  // error
}

// Vacuity control: the positive control's context is consistent.
lemma Vacuity(f: int ~> int)
  requires f.requires(10)
{
  var g := f.requires;
  assert g.reads(10) == f.reads(10);
  assert false;  // error
}
