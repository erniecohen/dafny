// The errors of the module graph, which are found before any module is resolved.  A copying import
// depends on the module it copies, as an ordinary import does.  So a module cannot copy itself, a
// module that encloses it, or a module that copies it.

module Self {
  import X := Self  // error: cycle
}

module Outer {
  module Inner {
    import X := Outer  // error: cycle
  }
}

module A {
  import X := B  // error: cycle
}

module B {
  import Y := A
}

module C {
  import X := Missing  // error: no such module
}
