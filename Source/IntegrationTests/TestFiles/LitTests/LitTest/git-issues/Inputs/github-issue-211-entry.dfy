// The caller's global bound is captured on entry.  Here it reads a mutable field.  Each call goes
// through a receiver of the trait type, so it is dispatched; this.Run() would bind statically.
module Tr {
  trait {:termination false} {:termination_closed} T {
    var fuel: nat
    method Run() modifies this decreases {:global} fuel
  }
}

module Impl {
  import Tr
  class Down extends Tr.T {
    // fuel - 1 is below the bound on entry, though not below the current fuel: verifies.
    method Run() modifies this {
      if fuel != 0 {
        fuel := fuel - 1;
        var t: Tr.T := this; t.Run();
      }
    }
  }
  class Up extends Tr.T {
    // fuel + 1 is above the bound on entry: rejected.
    method Run() modifies this {
      fuel := fuel + 1;
      var t: Tr.T := this; t.Run();
    }
  }
  class Same extends Tr.T {
    // The same bound is not below it: rejected.
    method Run() modifies this {
      var t: Tr.T := this; t.Run();
    }
  }
  class Vacuous extends Tr.T {
    // Vacuity control: the checked call holds, and the assert false after it fails.
    method Run() modifies this {
      if fuel != 0 {
        fuel := fuel - 1;
        var t: Tr.T := this; t.Run();
        assert false;
      }
    }
  }
}
