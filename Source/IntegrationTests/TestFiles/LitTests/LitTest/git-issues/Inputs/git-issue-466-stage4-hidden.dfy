// The private synonym is part of the cone even where its module only provides it.
module Owner {
  import Helper
  datatype Liar = Liar(n: int) {
    invariant Helper.No()
  }
}
module Helper {
  import Owner
  export provides U, WouldBeBad reveals No
  type U(!new) = Owner.Liar
  ghost predicate No() { !(exists x: U {:nowarn} :: true) }
  lemma WouldBeBad() ensures false {
    if No() {
      var raw: raw Owner.Liar := Owner.Liar(0);
      assert raw is Owner.Liar;
      var x: U := raw;
      assert exists y: U {:nowarn} :: y == x;
      assert exists y: U {:nowarn} :: true;
    } else {
      var x: U :| true;
      assert No();
    }
  }
}
