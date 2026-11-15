module M {
  datatype D = D {
    function {:operator "<"} less(other: D): (r: bool) ensures r { true }
  }
  lemma rank_irreflexive(x: D) ensures !(x < x) { }
  export provides D, D.less, rank_irreflexive
}
module Client {
  import M
  lemma bad(x: M.D) {
    M.rank_irreflexive(x);
    assert false;
  }
}
