# Pure callback allocation in the invocation heap

Issue #132 removes the converse from an allocated reads frame to an allocated
function value. A closure can capture a reference without reading its fields,
so that converse could allocate the closure before its captured object existed.

The native result rule establishes allocation of `Apply(h, f, args)` in a good
heap `h`, given an allocated function, allocated arguments and a satisfied
precondition. Pure arrow applications instead use `Apply($OneHeap, f, args)`.
Native rules transport empty reads and preconditions between these heaps, but
application frame equality requires heap succession. There is no required
succession relationship between `$OneHeap` and the invocation heap. The result
allocation consequence below is therefore an addition to the native theory.

## Model argument

Interpret a pure function value as a closure whose result, on arguments satisfying
its precondition, is independent of the invocation heap. Its allocation in a heap
requires allocation of the values it captures and admission of its captured
previous or labeled heaps. A well-formed pure invocation does not allocate a new
object: its returned references come from the available closure, arguments and
admitted captures. Its semantic result is therefore allocated in a good invocation
heap where that function and its arguments are allocated. The pure selector at
`$OneHeap` denotes that same result, so the consequence establishes allocation in
the invocation heap; it does not establish allocation in `$OneHeap`.

The translator exports the consequence only within an existing source lambda
permission and only for a statically partial or total arrow application. Its
premises retain:

- the actual good invocation heap, distinct from the canonical selector heap;
- native function membership and allocation in the invocation heap;
- membership and allocation of every argument in that heap;
- the pure selector's precondition and empty reads frame;
- allocation of reference-bearing free values and the receiver in that heap;
- equality or succession from every captured previous or labeled heap.

The enclosing lambda's exact family, typed and allocated formals, source range,
and formation/future-heap guards remain in place. Without an actual invocation
heap, the translator supplies no fact. It does not infer function allocation from
empty reads, transport allocation into an earlier heap, or posit succession from
`$OneHeap`.

## Default inclusion and controls

The repository owner approved default inclusion of this guarded consequence as a
specific exception to the shared additional-axioms policy for the allocation
repair. The other additional axioms retain their option. This consequence keeps
valid generic callback and parser composition proofs available after removal of
the unsound converse.

`github-issue-132-pure-callback.dfy` checks both resolvers with additional axioms
both disabled and enabled. A generic callback composition and its current-heap
allocation remain provable. A closure capturing a fresh reference must fail its
old-heap allocation assertion. A separately inhabited callback context must fail
only its final `assert false`. The original #132 and #82 controls remain registered.

These controls supplement the model argument; they do not establish consistency
of the complete backend theory. Current-base focused and full CI evidence is
required before merging this consequence.
