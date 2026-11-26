// Exercise group constructors and an eager trait base from a separate client.
module Client {
  import Alpha
  import Zulu
  method Main() {
    var ts := new Alpha.T[1];
    print Alpha.G(3), " ", Zulu.Start, " ", ts[0].t.n, "\n";
    print Alpha.T(Zulu.T(7)), "\n";
    var item := new Zulu.Item();
    print Zulu.Make().t.n, " ", item.Get(), "\n";
  }
}
