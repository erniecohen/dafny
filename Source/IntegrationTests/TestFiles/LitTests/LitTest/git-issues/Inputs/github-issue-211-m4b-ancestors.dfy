module Library {
  trait Sealed { }
  trait {:termination false} Open extends Sealed { }
  trait Child extends Open { }
  trait {:termination false} Grandchild extends Child { }
}
