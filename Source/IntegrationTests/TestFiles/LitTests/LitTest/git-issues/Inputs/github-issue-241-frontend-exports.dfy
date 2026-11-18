module Exported {
  export reveals Twice
  function Twice(n: nat): nat { n + n }
}

module Client {
  import E = Exported
  function F(n: nat): nat { E.Twice(n) }
}
