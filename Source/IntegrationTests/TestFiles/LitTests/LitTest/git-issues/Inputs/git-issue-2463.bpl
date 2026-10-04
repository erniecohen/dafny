// Appended to the Boogie program that Dafny emits, with its prelude.  Under the arguments type
// encoding, which Dafny uses, heaps are not extensional; under the monomorphic one, heaps are
// native, extensional arrays, so restoring a cell gives back the same heap.

// The issue: allocate an unallocated reference, then write back the box it held.  The second
// write deallocates, and the unguarded axiom made it a succession step, which together with
// the monotonicity of alloc proved false.
procedure Issue2463_Restore(h: Heap, r: ref)
{
  var b0: Box;
  var h1, h2: Heap;
  assume $IsGoodHeap(h);
  b0 := read(h, r, alloc);
  assume !($Unbox(b0): bool);
  h1 := update(h, r, alloc, $Box(true));
  h2 := update(h1, r, alloc, b0);
  assert h2 == h;  // extensionality, under the monomorphic encoding only
  assert false;
}

// A write that deallocates is not a succession step.
procedure Issue2463_Deallocate(h: Heap, r: ref)
{
  var h1: Heap;
  assume $IsGoodHeap(h);
  assume $Unbox(read(h, r, alloc)): bool;
  h1 := update(h, r, alloc, $Box(false));
  assume $IsGoodHeap(h1);
  assert $HeapSucc(h, h1);
}

// Allocation is a succession step.
procedure Issue2463_Allocate(h: Heap, r: ref)
{
  var h1: Heap;
  assume $IsGoodHeap(h);
  assume !($Unbox(read(h, r, alloc)): bool);
  h1 := update(h, r, alloc, $Box(true));
  assume $IsGoodHeap(h1);
  assert $HeapSucc(h, h1);
  assert $Unbox(read(h1, r, alloc)): bool;
}

// So is allocating a reference that is allocated already.
procedure Issue2463_Reallocate(h: Heap, r: ref)
{
  var h1: Heap;
  assume $IsGoodHeap(h);
  assume $Unbox(read(h, r, alloc)): bool;
  h1 := update(h, r, alloc, $Box(true));
  assume $IsGoodHeap(h1);
  assert $HeapSucc(h, h1);
}

// So is a write to any other field, and to an array element.
procedure Issue2463_OtherFields(h: Heap, r: ref, f: Field, i: int, x: Box)
{
  var h1, h2: Heap;
  assume $IsGoodHeap(h);
  assume f != alloc;
  h1 := update(h, r, f, x);
  assume $IsGoodHeap(h1);
  assert $HeapSucc(h, h1);
  h2 := update(h1, r, IndexField(i), x);
  assume $IsGoodHeap(h2);
  assert $HeapSucc(h1, h2);
}
