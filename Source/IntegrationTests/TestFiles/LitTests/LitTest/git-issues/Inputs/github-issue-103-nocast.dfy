// https://github.com/erniecohen/dafny/issues/103: the inherited predicate p, proved through the overriding has, without a cast.
module Ma {
trait {:termination false} A { 
    ghost predicate has(d:int,b:bool) decreases this
    ghost predicate p(d:int) { forall b :: has(d,b) }
    function top():(d:int) ensures p(d)
}  
} module Mb { import opened Ma  // a different module: Ma.A.has is not fuel-aware
datatype C extends A = C(a:A) {
    ghost predicate has(d:int,b:bool) decreases this { a.has(d,b) }    
    function top():(r:int) decreases this ensures p(r) {

        var r := a.top();
        assert p(r) by {
            forall b ensures this.has(r,b) { 
                assert has(r,b);
            }
        }
        r
}}}
