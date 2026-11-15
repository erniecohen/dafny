lemma L(a:array<int>,i:nat) requires i<a.Length { assert old(allocated(a)); var x := old(a[i]); }
