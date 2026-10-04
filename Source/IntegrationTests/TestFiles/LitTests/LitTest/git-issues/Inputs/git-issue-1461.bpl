// Appended to the Boogie program that Dafny emits with --additional-axioms, with its prelude.  With the
// option, Dafny assumes  $IsGoodHeap(h) ==> $HeapSucc(h, h)  at the previous heap h of each two-state
// function used as a value.  These procedures assume that instance at every heap they make, and check
// that it is consistent with the update axioms of erniecohen/dafny#81.  Under the monomorphic type
// encoding, heaps are native, extensional arrays.

// The scenario of #81: allocate an unallocated reference, then write back the box it held.  The heap
// this gives back is the original one under the monomorphic encoding, and so a successor of itself.
procedure Issue1461_Restore(h: Heap, r: ref)
{
  var b0: Box;
  var h1, h2: Heap;
  assume $IsGoodHeap(h);
  assume $IsGoodHeap(h) ==> $HeapSucc(h, h);
  b0 := read(h, r, alloc);
  assume !($Unbox(b0): bool);
  h1 := update(h, r, alloc, $Box(true));
  assume $IsGoodHeap(h1) ==> $HeapSucc(h1, h1);
  h2 := update(h1, r, alloc, b0);
  assume $IsGoodHeap(h2) ==> $HeapSucc(h2, h2);
  assert h2 == h;  // extensionality, under the monomorphic encoding only
  assert $HeapSucc(h2, h);
  assert false;
}

// An allocation, with the instance at both heaps.
procedure Issue1461_Allocate(h: Heap, r: ref)
{
  var h1: Heap;
  assume $IsGoodHeap(h);
  assume $IsGoodHeap(h) ==> $HeapSucc(h, h);
  assume !($Unbox(read(h, r, alloc)): bool);
  h1 := update(h, r, alloc, $Box(true));
  assume $IsGoodHeap(h1);
  assume $IsGoodHeap(h1) ==> $HeapSucc(h1, h1);
  assert $HeapSucc(h, h1) && $HeapSucc(h1, h1);
  assert false;
}
