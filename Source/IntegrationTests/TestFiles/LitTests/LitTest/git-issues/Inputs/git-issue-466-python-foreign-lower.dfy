module Lower { trait T { } }
module A {
  import B
  import Lower
  class {:extern "E"} C extends Lower.T { constructor () { } }
}
module B { import A method Read() { var c := new A.C(); print "1\n"; } }
module C {
  import D
  import Lower
  class {:extern "E2"} C extends Lower.T { constructor () { } }
}
module D { import C method Read() { var c := new C.C(); } }
method Main() { B.Read(); D.Read(); }
