module Ma {
trait {:termination false} A {
    ghost predicate has(d:int,b:bool) decreases this
    ghost predicate p(d:int) { forall b :: has(d,b) }
    function top():(d:int) ensures p(d)
}

datatype C extends A = C(a:A) {
    ghost predicate has(d:int,b:bool) decreases this { a.has(d,b) }
    function top():(r:int) decreases this ensures p(r) {
        var ad := this as A;
        var r := a.top();
        assert p(r) by {
            forall b ensures ad.has(r,b) {
                assert has(r,b);
            }
        }
        r
}}}
