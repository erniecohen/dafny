include "github-issue-211-m4b-refinement.dfy"
module Controls {
  import R = Realization
  method Refined(t: R.T) decreases {:global} 1 { t.F(0); R.Closed(); assert false; }
}
