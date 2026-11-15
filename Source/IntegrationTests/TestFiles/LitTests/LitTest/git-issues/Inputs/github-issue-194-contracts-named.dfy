datatype D = D(n: int) {
  function {:operator "/"} divide(k: int): D requires k != 0 { D(n / k) }
}
lemma bad() {
  var d := D(1);
  var result := d.divide(0);
}
