// Both the rejected declaration and its legacy false proof exercise a type test.
trait Tr { }
datatype Item extends Tr = Item
type Empty = x: Tr | false witness *
datatype Box<-X extends Tr> = Box(o: Tr)
type S<-X extends Tr> = s: Box<X> | s.o is X witness *
lemma Contradiction(t: S<Empty>) ensures false { assert t.o is Empty; assert false; }
lemma FalseProof() {
  var s: S<Tr> := Box(Item());
  var t: S<Empty> := s;
  Contradiction(t);
  assert false;
}
