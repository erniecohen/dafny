// Appended to the Boogie program that Dafny emits with --additional-axioms, with its prelude.  Under
// the monomorphic type encoding, heaps are native, extensional arrays.

procedure Issue1461_Reflexive(h: Heap)
{
  assume $IsGoodHeap(h);
  assert $HeapSucc(h, h);
}

// A write of the box a reference holds gives back the same heap, under the monomorphic encoding.
procedure Issue1461_WriteBack(h: Heap, r: ref)
{
  var h1: Heap;
  assume $IsGoodHeap(h);
  assume $Unbox(read(h, r, alloc)): bool;
  h1 := update(h, r, alloc, read(h, r, alloc));
  assume $IsGoodHeap(h1);
  assert $HeapSucc(h, h1);
}

// Transitivity does not apply to equal end points, and reflexivity covers them.
procedure Issue1461_Transitive(h: Heap, r: ref, f: Field, i: int, x: Box)
{
  var h1, h2: Heap;
  assume $IsGoodHeap(h);
  assume f != alloc;
  h1 := update(h, r, f, x);
  assume $IsGoodHeap(h1);
  h2 := update(h1, r, IndexField(i), x);
  assume $IsGoodHeap(h2);
  assert $HeapSucc(h, h2);
}

// Only good heaps.
procedure Issue1461_NotGood(h: Heap)
{
  assert $HeapSucc(h, h);
}

// Vacuity, together with the guard of erniecohen/dafny#81: the restoring write gives back the
// heap that held the unallocated reference, which is good, and it succeeds itself.
procedure Issue1461_Restore(h: Heap, r: ref)
{
  var b0: Box;
  var h1, h2: Heap;
  assume $IsGoodHeap(h);
  b0 := read(h, r, alloc);
  assume !($Unbox(b0): bool);
  h1 := update(h, r, alloc, $Box(true));
  h2 := update(h1, r, alloc, b0);
  assert h2 == h;  // extensionality, under the monomorphic encoding only
  assert $HeapSucc(h2, h);
  assert false;
}
