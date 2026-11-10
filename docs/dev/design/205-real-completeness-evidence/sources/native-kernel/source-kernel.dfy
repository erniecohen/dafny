// Experimental trusted placeholder for a proposed intrinsic; not a product implementation.
module RealCompletenessProbe {
  ghost predicate upper(s: iset<real>, b: real) {
    forall x: real {:trigger x in s} {:qid "issue205_upper_point"} :: x in s ==> x <= b
  }

  // Raw guarded semantic instance; the public operation checks both premises.
  lemma {:axiom} complete(s: iset<real>, w: real, b: real) returns (r: real)
    ensures (w in s && upper(s, b)) ==> upper(s, r)
    ensures (w in s && upper(s, b)) ==>
      forall u: real {:trigger upper(s, u)} {:qid "issue205_least"} :: upper(s, u) ==> r <= u

  lemma lub(s: iset<real>, w: real, b: real) returns (r: real)
    requires w in s
    requires upper(s, b)
    ensures upper(s, r)
    ensures forall u: real {:trigger upper(s, u)} :: upper(s, u) ==> r <= u
  {
    r := complete(s, w, b);
  }

  lemma upperUse(s: iset<real>, w: real, b: real, x: real)
    requires w in s && upper(s, b) && x in s
  {
    var r := lub(s, w, b);
    assert x <= r;
    assert w <= r <= b;
  }

  lemma leastUse(s: iset<real>, w: real, b: real, c: real)
    requires w in s && upper(s, b) && upper(s, c)
  {
    var r := lub(s, w, b);
    assert r <= c;
  }

  lemma existsUse(s: iset<real>, w: real, b: real)
    requires w in s && upper(s, b)
    ensures exists r: real :: upper(s, r) && forall c: real :: upper(s, c) ==> r <= c
  {
    var r := lub(s, w, b);
  }

  lemma singletonFalse()
  {
    var s := iset{0.0};
    assert upper(s, 1.0);
    var r := complete(s, 0.0, 1.0);
    assert upper(s, 0.0);
    assert r == 0.0;
    assert false; // expected failure after live bound and leastness facts
  }

  lemma emptyFalse()
  {
    var s: iset<real> := iset{};
    assert upper(s, 0.0);
    var r := complete(s, 0.0, 0.0);
    assert false; // expected failure; false nonempty guard
  }

  lemma universalFalse()
  {
    var s := iset x: real | true;
    assert 1.0 in s;
    assert !upper(s, 0.0);
    var r := complete(s, 0.0, 0.0);
    assert false; // expected failure; false boundedness guard
  }

  lemma emptyValue()
  {
    var r := complete(iset{}, 0.0, 0.0);
    assert r == 0.0; // expected failure; unspecified fallback remains unconstrained
  }

  lemma universalValue()
  {
    var r := complete(iset x: real | true, 0.0, 0.0);
    assert r == 0.0; // expected failure; unspecified fallback remains unconstrained
  }
}
