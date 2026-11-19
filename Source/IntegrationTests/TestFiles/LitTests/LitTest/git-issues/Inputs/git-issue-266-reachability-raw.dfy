// The same four reachability paths with raw arrows, which carry no formation evidence.
module DatatypeArgument {
  datatype Box = Box(c: C)
  class C {
    const cb: Box -> int
    constructor (h: Box -> int) { cb := h; } // rejected through Box.c
  }
}
module ConstantArgument {
  class D {
    const c: C
    constructor (c: C) { this.c := c; }
  }
  class C {
    const cb: D -> int
    constructor (h: D -> int) { cb := h; } // rejected through D.c
  }
}
module DatatypeStorage {
  datatype Wrap = Wrap(cb: C -> int)
  class C {
    const wrapped: Wrap
    constructor (w: Wrap) { wrapped := w; } // rejected through Wrap.cb
  }
}
module ConstantStorage {
  class D {
    const cb: C -> int
    constructor (h: C -> int) { cb := h; } // rejected through C.holder
  }
  class C {
    const holder: D
    constructor (d: D) { holder := d; } // rejected through D.cb
  }
}
