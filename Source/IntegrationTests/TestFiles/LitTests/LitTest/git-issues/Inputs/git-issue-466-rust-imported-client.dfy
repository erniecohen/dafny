module ImportedClient {
  import RefinedImported
  import RefinedAgain
  import ImportedBase
  import Nested.async
  import Client
  import A
  method Main() {
    Client.Main();
    var x: RefinedImported.Native := new RefinedImported.Native();
    var y: RefinedAgain.Native := new RefinedAgain.Native();
    var z: async.Native := new async.Native();
    print x.Read(), " ", y.Read(), " ", z.Read(), " ", RefinedImported.Count(4), " ", RefinedAgain.Count(5), "\n";
    var box := new RefinedImported.Generic<int>();
    var treeBox := new RefinedAgain.Generic<A.Tree<int>>();
    print box.Echo(43), " ", treeBox.Echo(A.Leaf(61)).value, "\n";
    var original := new ImportedBase.Native();
    print original.Read(), "\n";
  }
}
