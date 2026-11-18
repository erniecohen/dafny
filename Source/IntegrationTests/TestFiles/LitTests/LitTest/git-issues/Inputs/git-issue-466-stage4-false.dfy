include "git-issue-466-stage4-positive.dfy"
module Control {
  import Trees
  import Forests
  import First
  import Third
  import Classes
  lemma TwoModules() {
    var t := Trees.Unit();
    var f := Forests.Unit();
    assert t.n >= 0;
    assert Forests.AllTrees(f.trees);
    assert false;
  }
  lemma ThreeModules() {
    var t := First.Unit();
    var v := Third.Unit();
    assert t.End? && t.n == 0;
    assert Third.Good(v);
    assert false;
  }
  method Class() {
    var c := Classes.Make();
    assert c.n >= 0;
    assert false;
  }
}
