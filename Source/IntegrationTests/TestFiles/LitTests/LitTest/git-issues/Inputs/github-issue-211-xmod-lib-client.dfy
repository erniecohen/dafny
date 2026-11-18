// A client of the library github-issue-211-xmod-lib.dfy.
module Client { import Lib import Impl
  method Main() { var c := new Impl.C(); c.Step(3); assert c.Value() == 7; }
}
