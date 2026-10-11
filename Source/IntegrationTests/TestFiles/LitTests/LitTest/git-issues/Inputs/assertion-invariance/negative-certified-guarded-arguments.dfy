ghost function F(i:int):int requires i>0 { i }
lemma L() ensures forall x:int :: x>0 ==> F(x)==x+1 {}
