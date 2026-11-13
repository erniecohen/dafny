include "github-issue-211-m4b-views.dfy"
module Controls {
  import P = Library`P
  import R = Library`R
  method Provided(t: P.T) decreases {:global} 1 { t.Virtual(0); P.Closed(); assert false; }
  method Revealed(t: R.T) decreases {:global} 1 { t.Virtual(0); R.Closed(); assert false; }
  method Fixed(t: P.T) { var n := t.Final(); assert false; }
}
