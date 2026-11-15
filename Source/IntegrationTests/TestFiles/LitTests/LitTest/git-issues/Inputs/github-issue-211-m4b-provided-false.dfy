include "github-issue-211-m4b-provided.dfy"
module Controls {
  import Library
  method Strict(t: Library.T) decreases {:global} 1 { t.Step(); assert false; }
}
