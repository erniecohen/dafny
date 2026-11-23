lemma Norm(n:nat,a:int) { IntegerBitwiseModel.unfoldNorm(n,a); }
lemma Shl(n:nat,a:int) requires 0 < n { IntegerBitwiseModel.unfoldShl(n,a,0); }
lemma Shr(n:nat,a:int) requires 0 < n { IntegerBitwiseModel.unfoldShr(n,a,0); }
lemma Rol(n:nat,a:int,k:int) requires 0 < n { IntegerBitwiseModel.unfoldRol(n,a,k); }
lemma Ror(n:nat,a:int,k:int) requires 0 < n { IntegerBitwiseModel.unfoldRor(n,a,k); }
lemma Width() { IntegerBitwiseModel.unfoldP(-1); }
