module {:extern "Imported"} ImportedBase {
  import ImportedPeer
  class {:extern "Native"} Native {
    constructor {:extern} ()
    function {:extern} Read(): int
  }
  class {:extern "Box"} Generic<T> {
    constructor {:extern} ()
    function {:extern} Echo(x: T): (r: T) ensures r == x
  }
  function {:extern "Foreign.Tools", "Identity"} Identity(x: int): (r: int) ensures r == x
  function Count(n: nat): nat { if n == 0 then 0 else Identity(1) + ImportedPeer.Count(n - 1) }
}
module ImportedPeer {
  import ImportedBase
  function Count(n: nat): nat { if n == 0 then 0 else 1 + ImportedBase.Count(n - 1) }
}
module RefinedImported refines ImportedBase { }
abstract module AbstractImported refines RefinedImported { }
module RefinedAgain refines AbstractImported { }
module Nested {
  module async refines ImportedBase { }
}
