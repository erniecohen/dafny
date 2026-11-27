module {:extern} M {
  class {:extern} C<T(0)> {
    constructor {:extern} (ghost erased: int, value: T, n: int)
    function {:extern} Read(): T
  }
}
method Main() { var c := new M.C<int>(0, 3, 5); print c.Read(); }
