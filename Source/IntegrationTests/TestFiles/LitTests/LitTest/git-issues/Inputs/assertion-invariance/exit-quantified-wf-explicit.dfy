ghost function F(i:int):int requires i>0 { i }
lemma L() ensures forall i:int {:trigger F(i)} :: i>0 ==> F(i)>0 { assert forall i:int {:trigger F(i)} :: i>0 ==> F(i)>0; }
