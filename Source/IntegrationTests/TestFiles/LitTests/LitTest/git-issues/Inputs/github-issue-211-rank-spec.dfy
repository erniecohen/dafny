// Milestone M4a of --modular-termination (https://github.com/erniecohen/dafny/issues/211): the routes of
// github-issue-211-rank-gap.dfy through a bounded member's specification rather than its bound.  The rank R
// reaches a dispatch of T.F, so its bound (8) is above T.F's (7); each member below calls R in its requires,
// ensures or reads clause, at its own inherited bound 7, and the same-bound rule rejects that call.  No member
// verifies vacuously: the error is at the call of R in the specification.

module Tr {
  trait {:termination false} T {
    function {:fuel 3,4} F(n: nat): nat
      decreases {:global} 7
    method M(n: nat) returns (r: nat)
      decreases {:global} 7
  }
}

module Z {
  import Tr
  function {:fuel 3,4} R(x: Tr.T): nat
    decreases {:global} 8
  {
    x.F(0)
  }
}

module Requires {
  import Tr
  import Z
  class C extends Tr.T {
    constructor () {}
    function {:fuel 3,4} F(n: nat): nat
      requires Z.R(this) >= 0
    {
      var me: Tr.T := this;
      me.F(n) + 1
    }
    method M(n: nat) returns (r: nat) { r := 0; }
  }
}

module Ensures {
  import Tr
  import Z
  class C extends Tr.T {
    constructor () {}
    function {:fuel 3,4} F(n: nat): nat
      ensures Z.R(this) >= 0
    {
      var me: Tr.T := this;
      me.F(n) + 1
    }
    method M(n: nat) returns (r: nat) { r := 0; }
  }
}

module Reads {
  import Tr
  import Z
  class C extends Tr.T {
    constructor () {}
    function {:fuel 3,4} F(n: nat): nat
      reads if Z.R(this) == 0 then {} else {}
    {
      var me: Tr.T := this;
      me.F(n) + 1
    }
    method M(n: nat) returns (r: nat) { r := 0; }
  }
}

module MethodRequires {
  import Tr
  import Z
  class C extends Tr.T {
    constructor () {}
    function {:fuel 3,4} F(n: nat): nat { 0 }
    method M(n: nat) returns (r: nat)
      requires Z.R(this) >= 0
    {
      var me: Tr.T := this;
      r := me.M(n);
    }
  }
}

module MethodEnsures {
  import Tr
  import Z
  class C extends Tr.T {
    constructor () {}
    function {:fuel 3,4} F(n: nat): nat { 0 }
    method M(n: nat) returns (r: nat)
      ensures Z.R(this) >= 0
    {
      var me: Tr.T := this;
      r := me.M(n);
    }
  }
}
