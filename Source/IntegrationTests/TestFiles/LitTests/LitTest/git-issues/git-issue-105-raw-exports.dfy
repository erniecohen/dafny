// Member invariants (https://github.com/erniecohen/dafny/issues/105): 'raw' across modules.  Where a
// datatype with invariants is revealed, a client sees both 'raw D' and 'D'.  Where it is only
// provided, it is an abstract type, so 'raw' does not yet apply to it (in the client, and in the
// export set's own signature), but its members, raw or not, may be used.
// RUN: %exits-with 2 %resolve --member-invariants --print-mode NoIncludes --rprint:- "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module A {
  export Provides provides Interval, Interval.Width, Interval.Ok, Make
  export Reveals reveals Interval provides Interval.Width, Interval.Ok, Make

  datatype Interval<X> = Interval(lo: int, hi: int, x: X) {
    predicate Ok() { lo <= hi }
    invariant Ok()
    function Width(): int { hi - lo }
  }

  function Make<X>(x: X): Interval<X> { Interval(0, 1, x) }
}

module ProvidedClient {
  import A`Provides

  method M(i: A.Interval<bool>) returns (w: int, b: bool) {
    var r := A.Make(true);
    w := i.Width() + r.Width();
    b := i.Ok() && r.Ok();
    var s := [i, r];
  }
}

module RevealedClient {
  import A`Reveals

  method M(i: A.Interval<int>, r: raw A.Interval<int>) returns (w: int, b: bool) {
    var k := A.Interval(1, 2, 3);
    w := i.Width() + k.Width();
    b := r.Ok() && k.Ok();
    var j: A.Interval<int> := k;
    var u := i.(lo := 0);
  }
}

module ProvidedRaw {
  import A`Provides

  method M(r: raw A.Interval<int>) { }
}

module ExportsRaw {
  export provides Interval, MakeRaw

  datatype Interval = Interval(lo: int, hi: int) {
    invariant lo <= hi
  }

  function MakeRaw(): raw Interval { Interval(1, 0) }
}
