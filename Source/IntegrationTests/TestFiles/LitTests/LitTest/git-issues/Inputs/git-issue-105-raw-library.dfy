module RawLib {
  datatype Interval = Interval(lo: int, hi: int) {
    invariant Ok()
    predicate Ok() { lo <= hi }
    function Width(): int { hi - lo }
  }

  function Make(lo: int, hi: int): raw Interval { Interval(lo, hi) }

  function Fix(r: raw Interval): Interval {
    if r.Ok() then r else Interval(r.hi, r.lo)
  }
}
