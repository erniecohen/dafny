ghost function F(i:int):int requires i>0 { i }
lemma Use() requires forall i:int {:trigger F(i)} :: i>0 ==> F(i)>0 {}
lemma L() { assert false; Use(); }
