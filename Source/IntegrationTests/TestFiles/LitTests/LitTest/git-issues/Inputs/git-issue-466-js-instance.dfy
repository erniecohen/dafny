module EntryPoint {
  import Partner
  class Program {
    method Main() { print Partner.Number, "\n"; }
  }
}
module Partner {
  import EntryPoint
  const Number: int := 9
}
