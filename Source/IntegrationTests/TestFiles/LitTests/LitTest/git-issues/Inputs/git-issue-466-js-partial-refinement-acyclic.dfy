module {:extern "ForeignBase"} A {
  class {:extern} C<T(0)> {
    var Stored: T
    constructor (value: T) { Stored := value; }
    function {:extern} Read(): int
    function Local(): int { 3 }
  }
}
module {:extern "ForeignRefined"} Refined refines A { }
method Main() { var c := new Refined.C<int>(3); print c.Read(), c.Stored; }
