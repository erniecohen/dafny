// The name of a copy is the name of a module of the importing module.  These errors are found
// when the names of the modules are bound, before any module is resolved.

module Plain {
  function One(): int { 1 }
}

module Twice {
  import X := Plain
  import X := Plain  // error: duplicate name
}

module CopyAndImport {
  import X := Plain
  import X = Plain  // error: duplicate name
}
