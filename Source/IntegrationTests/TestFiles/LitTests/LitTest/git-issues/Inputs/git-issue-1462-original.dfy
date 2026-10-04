// The issue's program.
trait Ob {
    function rep():set<Ob> reads this
    twostate lemma l0() decreases rep() {
        if o :| o in rep() && old(allocated(o)) && o.rep() < rep() {
            o.l0();  // Error: failure to decrease termination measure
        }
    }
}
