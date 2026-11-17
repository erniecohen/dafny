// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-481-dispatch.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-481-dispatch.dfy" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-481-client.dfy" --library "%S/Inputs/github-issue-481-library.dfy" >> "%t"
// RUN: %build -t:lib "%S/Inputs/github-issue-481-library.dfy" --output "%t.doo" > "%t.build"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-481-client.dfy" --library "%t.doo" >> "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-481-client.dfy" --library "%t.doo" >> "%t"
// RUN: %exits-with 0 %verify "%S/Inputs/github-issue-481-positive.dfy" >> "%t"
// RUN: %exits-with 0 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-481-positive.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-generic-function-stores "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/481): a generic class could store a value of
// its type parameter in a field.  Instantiated with a function type that reads the heap, the stored
// function could read itself, and the program proved false.  The check that rejects storing functions
// with read effects in memory looks at the static type of the stored value, which in generic code is
// the type parameter.  Now the resolver infers which type parameters may be stored in memory, and
// rejects instantiating one with a type that contains a function with read effects.  An overriding
// member, or a declaration in a refining module, may store a type parameter only if what it overrides
// or refines may, or declares it with the characteristic (!reads)
// (Inputs/github-issue-481-dispatch.dfy).  The inference holds for a client in another module, also
// one whose import hides the bodies, and one that uses the library as a .doo file
// (Inputs/github-issue-481-client.dfy).  Instantiating with a type that reads nothing is accepted
// (Inputs/github-issue-481-positive.dfy).  The hidden --legacy-generic-function-stores restores the
// old, unsound rule, under which this file verifies.

// The reproducer: a class
module GenericStore {
  class Ref<T> {
    ghost var w: T
    constructor (ghost w: T) ensures this.w == w { this.w := w; }
    method Set(ghost t: T) modifies this ensures w == t { w := t; }
  }
  method Main() ensures false {
    var r := new Ref<() ~> bool>(() => true); // error
    ghost var knot := () reads * => if r.w.requires() then !r.w() else false;
    r.Set(knot);
    assert knot() == !knot();
  }
}

// A trait with a mutable field
module Trait {
  trait Store<T> extends object {
    ghost var w: T
    method Set(ghost t: T) modifies this ensures w == t { w := t; }
  }
  class C extends Store<() ~> bool> { // error
    constructor () { w := () => true; }
  }
  method Main() ensures false {
    var c := new C();
    ghost var knot := () reads * => if c.w.requires() then !c.w() else false;
    c.Set(knot);
    assert knot() == !knot();
  }
}

// The function wrapped in a datatype, in a sequence
module Wrapped {
  datatype Cell<A> = Cell(f: A)
  class Box<T> {
    ghost var s: seq<T>
    constructor () ensures s == [] { s := []; }
    method Push(ghost t: T) modifies this ensures s == old(s) + [t] { s := s + [t]; }
  }
  method Main() ensures false {
    var b := new Box<Cell<() ~> bool>>(); // error
    ghost var knot := () reads * => if |b.s| == 1 && b.s[0].f.requires() then !b.s[0].f() else false;
    b.Push(Cell(knot));
    assert knot() == !knot();
  }
}

// A class that holds the store in a constant
module Nested {
  class Ref<T> {
    ghost var w: T
    constructor (ghost w: T) ensures this.w == w { this.w := w; }
    method Set(ghost t: T) modifies this ensures w == t { w := t; }
  }
  class Holder<U> {
    const r: Ref<U>
    constructor (ghost u: U) ensures fresh(r) { r := new Ref(u); }
  }
  method Main() ensures false {
    var h := new Holder<() ~> bool>(() => true); // error
    ghost var knot := () reads * => if h.r.w.requires() then !h.r.w() else false;
    h.r.Set(knot);
    assert knot() == !knot();
  }
}

// A generic method that stores its type parameter through a generic class
module GenericMethod {
  class Ref<T> {
    ghost var w: T
    constructor (ghost w: T) ensures this.w == w { this.w := w; }
  }
  method Put<U>(r: Ref<U>, ghost u: U) modifies r ensures r.w == u { r.w := u; }
  method Main() ensures false {
    var r := new Ref<() ~> bool>(() => true); // error
    ghost var knot := () reads * => if r.w.requires() then !r.w() else false;
    Put(r, knot); // error
    assert knot() == !knot();
  }
}
