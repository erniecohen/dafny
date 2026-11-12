include "github-issue-211-m4b-good.dfy"
module Controls {
  import Library
  method Multiple(l: Library.Left, r: Library.Right) decreases {:global} 1 { l.L(); r.R(); assert false; }
  import Client
  method General(t: Library.T) decreases {:global} 4 { Client.General(t); assert false; }
  method Closed() decreases {:global} 0 { Client.Closed(); assert false; }
  method Rank() { var n := Client.Ranked(0); assert false; }
  method Constant() { var n := Client.Constant(); assert false; }
  method Types(s: Library.Small, d: Library.D) { Client.Types(s, d); assert false; }
  method LocalInference() { Client.LocalInference(); assert false; }
  method Callback() { var n := Client.Named(0); assert false; }
}
