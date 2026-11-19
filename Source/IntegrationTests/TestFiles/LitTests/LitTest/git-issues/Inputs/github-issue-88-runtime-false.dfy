include "../github-issue-88-runtime.dfy"

module Control {
  import Refined
  lemma FalseControl(c: Refined.C, d: Refined.D) {
    var s: Refined.S<int> := c;
    var t: Refined.S<int> := d;
    assert false;
  }
}
