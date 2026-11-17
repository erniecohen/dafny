// Part of github-issue-481.dfy: programs that are accepted.

// A stored type parameter instantiated with types that read nothing
module ReadsNothing {
  class Ref<T> {
    var w: T
    constructor (w: T) ensures this.w == w { this.w := w; }
    method Set(t: T) modifies this ensures w == t { w := t; }
  }
  datatype Cell<A> = Cell(f: A)
  class C {}
  method Main() {
    var r := new Ref<int -> int>(x => x);
    r.Set(x => x + 1);
    var c := new Ref<Cell<() -> bool>>(Cell(() => true));
    var c0 := new C;
    var o := new Ref<object>(c0);
    var arr := new int[3];
    var p := new Ref<array<int>>(arr);
    assert r.w(2) == 3;
  }
}

// A constant field does not store its type parameter
module Constant {
  class Box<T> {
    const v: T
    constructor (v: T) ensures this.v == v { this.v := v; }
  }
  method Main() {
    var b := new Box<() ~> bool>(() => true);
  }
}

// An array of a type that contains a reading function, written only with values that read nothing
module ArrayOfReads {
  datatype Pack<T> = Pack(ghost c: T)
  method R(r: array<Pack<() ~> bool>>) requires r.Length != 0 modifies r {
    var p: Pack<() -> bool> := Pack(() reads {} => false);
    r[0] := p;
  }
  method Fill<U>(a: array<U>, u: U) modifies a { if a.Length > 0 { a[0] := u; } }
  method Client(a: array<int -> int>) modifies a { Fill(a, x => x); }
}

// A trait method with the characteristic, overridden by a method that stores
module Marked {
  trait Tr {
    method Put<U(!reads)>(a: array<U>, u: U) modifies a requires a.Length > 0
  }
  class C extends Tr {
    method Put<U>(a: array<U>, u: U) modifies a requires a.Length > 0 { a[0] := u; }
  }
  method Client(t: Tr, a: array<int -> int>) modifies a requires a.Length > 0 {
    t.Put(a, x => x);
  }
}

// A refinement that stores what the abstract module allows
abstract module AM {
  class Ref<T(!reads)> {
    method Set(t: T) modifies this
  }
  method Put<U(!reads)>(a: array<U>, u: U) modifies a requires a.Length > 0
}
module BM refines AM {
  class Ref<T> ... {
    var w: T
    constructor (w: T) { this.w := w; }
    method Set(t: T) { w := t; }
  }
  method Put<U>(a: array<U>, u: U) { a[0] := u; }
}

// An abstract import keeps what the bodies of the imported module store, so a module that refines
// the importer with the concrete module stores no more than the importer
abstract module SCore {
  method Store<A>(a: array<A>, x: A) modifies a requires a.Length > 0 { a[0] := x; }
  datatype Box<R> = Box(r: R) {
    method Keep(a: array<R>) modifies a requires a.Length > 0 { Store(a, r); }
  }
}
module SCoreImpl refines SCore {}
abstract module SBuilders {
  import P : SCore
  datatype B<R> = B(p: P.Box<R>)
}
module SBuildersImpl refines SBuilders {
  import P = SCoreImpl
}
