// A negative result reached while visiting a cycle is not a completed dependency summary.
module Owner {
  import Helper
  datatype Liar = Liar(n: int) {
    invariant Helper.No()
  }
}
module Helper {
  import Owner
  datatype B = Loop(c: C) | End(t: Owner.Liar)
  datatype C = C(b: B)
  ghost predicate No() {
    (forall b: B | b in [] :: true) && !(exists c: C {:nowarn} :: true)
  }
  ghost function Extract(c: C): Owner.Liar {
    match c.b
    case End(t) => t
    case Loop(next) => Extract(next)
  }
  lemma HasValue(c: C) ensures exists x: C {:nowarn} :: true { }
  lemma WouldBeBad() ensures false {
    if No() {
      var t: Owner.Liar := Owner.Liar(0);
      var c := C(End(t));
      HasValue(c);
    } else {
      var c: C :| true;
      var t := Extract(c);
      assert No();
    }
  }
}
