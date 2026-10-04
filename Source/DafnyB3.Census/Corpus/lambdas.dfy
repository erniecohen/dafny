lemma Lambdas() {
 var f := (x: int) => x + 1; assert f(0) == 1;
 var s := set x: int | 0 <= x < 3 :: x;
 var m := map x: int | x in s :: x + 1;
 assert 1 in m && m[1] == 2;
}
