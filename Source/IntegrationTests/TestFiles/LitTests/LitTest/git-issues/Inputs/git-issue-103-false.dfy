// Vacuity control for https://github.com/erniecohen/dafny/issues/103: the new instance must not prove false.
module Ma {
trait {:termination false} A {
    ghost predicate has(d:int,b:bool) decreases this
    ghost predicate p(d:int) { forall b :: has(d,b) }
    function top():(d:int) ensures p(d)
}
} module Mb { import opened Ma
datatype C extends A = C(a:A) {
    ghost predicate has(d:int,b:bool) decreases this { a.has(d,b) }
    function top():(r:int) decreases this ensures p(r) {
        var r := a.top();
        assert p(r) by {
            forall b ensures this.has(r,b) {
                assert has(r,b);
            }
        }
        assert false;
        r
}}}
