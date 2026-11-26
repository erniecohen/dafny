module TraitChecks {
  import Client
  import A
  import B
  import RefinedB
  import Final
  method {:main} Main() {
    Client.Main();
    var b := B.Viewer<int>.Create(151);
    var r := RefinedB.Viewer<A.Tree<int>>.Create(A.Leaf(157));
    var f := Final.Viewer<int>.Create(163);
    assert b.Read() == 151 && r.Read().value == 157 && f.Read() == 163;
    var bv: B.View := b;
    var rv: RefinedB.View := r;
    var fv: Final.View := f;
    var bg: B.GenericView<int> := b;
    var rg: RefinedB.GenericView<A.Tree<int>> := r;
    var fg: Final.GenericView<int> := f;
    assert bg.GhostValue() == 1 && rg.GhostValue() == 1 && fg.GhostValue() == 1;
    print bv.Value(), " ", rv.Value(), " ", fv.Value(), "\n";
    print bg.Read(), " ", rg.Read().value, " ", fg.Read(), "\n";
    var bo: object := b;
    var ro: object := r;
    var fo: object := f;
    print bo is B.View, " ", bo is RefinedB.View, " ", bo is Final.View, "\n";
    print ro is B.View, " ", ro is RefinedB.View, " ", ro is Final.View, "\n";
    print fo is B.View, " ", fo is RefinedB.View, " ", fo is Final.View, "\n";
    var mirror := new Final.Mirror<int>(167);
    var mirrorView: Final.View := mirror;
    var mirrorGeneric: Final.GenericView<int> := mirror;
    print mirrorView.Value(), " ", mirrorGeneric.Read(), "\n";
    var mo: object := mirror;
    print mo is Final.View, " ", mo is B.View, "\n";
    var tagged: object := new Final.Tagged();
    print tagged is Final.LocalTag, " ", tagged is Final.OtherTag, "\n";
  }
}
