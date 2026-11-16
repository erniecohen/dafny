// Replacements that depend on no use of the module they replace.  Each is accepted, and the program
// verifies and runs.

// The replacement imports a module that does not use the replaced module (the control of the issue).
module Helper { function H(n: nat): nat { n } }
replaceable module Foo { function G(n: nat): nat }
module Bar { import Foo  function F(n: nat): nat { Foo.G(n) } }
module FooImpl replaces Foo { import Helper  function G(n: nat): nat { Helper.H(n) + 1 } }

// The replaced module is nested beside a client and a helper.  The replacement imports the helper by a
// qualified name, which is a dependency on the helper and not on the enclosing module or the client.
module Outer {
  replaceable module F { function G(n: nat): nat }
  module Aid { function H(n: nat): nat { n } }
  module Client { import F  function Use(n: nat): nat { F.G(n) } }
}
module OuterImpl replaces Outer.F { import Outer.Aid  function G(n: nat): nat { Aid.H(n) + 2 } }

// The replaced module has an import and a member with a body.  The replacement has them too, and
// uses them.
replaceable module WithImport {
  import Helper
  function G(n: nat): nat
  function D(n: nat): nat { Helper.H(n) }
}
module WithImportUser { import WithImport  function F(n: nat): nat { WithImport.G(n) } }
module WithImportImpl replaces WithImport { function G(n: nat): nat { Helper.H(n) + D(n) + 3 } }

// The replacement is nested in a client of the module it replaces.  The client depends on the
// replacement, and the replacement does not depend on the client.
replaceable module Inner { function G(n: nat): nat }
module Holder {
  import Inner
  module InnerImpl replaces Inner { function G(n: nat): nat { n + 4 } }
  function F(n: nat): nat { Inner.G(n) }
}

// A chain of replacements none of which uses a client.
replaceable module Low { function G(n: nat): nat }
replaceable module Mid replaces Low { function M(n: nat): nat }
module LowUser { import Low  function F(n: nat): nat { Low.G(n) } }
module Top replaces Mid { import Helper  function G(n: nat): nat { Helper.H(n) + 5 }  function M(n: nat): nat { n } }

method Main() {
  print Bar.F(0), " ", Outer.Client.Use(0), " ", WithImportUser.F(0), " ", Holder.F(0), " ", LowUser.F(0), "\n";
}
