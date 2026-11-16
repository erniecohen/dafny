// Member invariants (https://github.com/erniecohen/dafny/issues/105): 'raw' is a contextual keyword,
// the raw-type operator only before a type name and only under --member-invariants.  Everywhere else,
// and everywhere without the option, it is an ordinary identifier, so this program resolves the same
// with and without the option.
// RUN: %resolve "%s" > "%t"
// RUN: %resolve --member-invariants "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

module raw {
  type raw = int
  type Seq<raw> = seq<raw>
  datatype Box = Box(raw: raw)
}

module Client {
  import R = raw
  type raw<T> = seq<T>
  datatype D = D(raw: R.raw) {
    function Raw(): raw<int> { [raw] }
  }

  method M(raw: raw<int>) returns (r: R.raw, s: R.Seq<R.raw>, b: R.Box)
    ensures r == |raw|
  {
    r := |raw|;
    s := raw;
    b := R.Box(r);
    var x: raw<R.raw> := [b.raw];
    var raw := D(r).raw;
  }

  newtype Small = raw: int | 0 <= raw < 10 witness 3
}
