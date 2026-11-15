module Library { method HiddenLeaf() { } }
module Client {
  import Library
  method Bounded() decreases {:global} 0 { Library.HiddenLeaf(); }
}
