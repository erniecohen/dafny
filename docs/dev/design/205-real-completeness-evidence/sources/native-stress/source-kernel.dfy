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

  lemma denseIntervalFalse()
  {
    var s := iset x: real | 0.0 <= x < 1.0;
    assert 0.0 in s && upper(s, 1.0);
    var r := complete(s, 0.0, 1.0);
    assert 0.0 <= r <= 1.0;
    assert false; // expected failure with dense infinite bounded set
  }

  lemma intervalWithOtherMembers(t: iset<real>)
    requires 1000.0 in t && 1001.0 in t && 1002.0 in t && 1003.0 in t && 1004.0 in t && 1005.0 in t && 1006.0 in t && 1007.0 in t && 1008.0 in t && 1009.0 in t && 1010.0 in t && 1011.0 in t && 1012.0 in t && 1013.0 in t && 1014.0 in t && 1015.0 in t && 1016.0 in t && 1017.0 in t && 1018.0 in t && 1019.0 in t && 1020.0 in t && 1021.0 in t && 1022.0 in t && 1023.0 in t && 1024.0 in t && 1025.0 in t && 1026.0 in t && 1027.0 in t && 1028.0 in t && 1029.0 in t && 1030.0 in t && 1031.0 in t && 1032.0 in t && 1033.0 in t && 1034.0 in t && 1035.0 in t && 1036.0 in t && 1037.0 in t && 1038.0 in t && 1039.0 in t && 1040.0 in t && 1041.0 in t && 1042.0 in t && 1043.0 in t && 1044.0 in t && 1045.0 in t && 1046.0 in t && 1047.0 in t && 1048.0 in t && 1049.0 in t && 1050.0 in t && 1051.0 in t && 1052.0 in t && 1053.0 in t && 1054.0 in t && 1055.0 in t && 1056.0 in t && 1057.0 in t && 1058.0 in t && 1059.0 in t && 1060.0 in t && 1061.0 in t && 1062.0 in t && 1063.0 in t
  {
    var s := iset x: real | 0.0 <= x < 1.0;
    assert 0.0 in s && upper(s, 1.0);
    var r := complete(s, 0.0, 1.0);
    assert 0.0 <= r <= 1.0;
    assert false; // expected failure; unrelated set membership must not drive bounds for s
  }
}
