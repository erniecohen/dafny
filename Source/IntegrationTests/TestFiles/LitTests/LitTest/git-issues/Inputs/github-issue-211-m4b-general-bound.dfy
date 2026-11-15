module Library { method Leaf() decreases {:global} 2 { } }
module Client {
  import Library
  method Bounded() decreases {:global} 1 { Library.Leaf(); }
}
