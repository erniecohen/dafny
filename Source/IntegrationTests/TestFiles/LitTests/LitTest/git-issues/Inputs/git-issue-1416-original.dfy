// The issue's program.
class O { const f:nat }
function f():set<O>
method test() {
    assert forall o:O | o in f() :: o.f >= 0;      // no complaint here - o sure looks allocated
    assert forall o:O | o in f() :: allocated(o); // fails
}
