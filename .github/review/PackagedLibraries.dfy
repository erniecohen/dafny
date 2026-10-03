module PackagedStandardLibrarySmoke {
  import opened Std.Wrappers
  import Std.FileIO

  method Main() {
    var wrapped := Some(7);
    assert wrapped.Some?;
    assert wrapped.value == 7;
    print wrapped.value, "\n";
    var written := FileIO.WriteBytesToFile("package-smoke.bin", [1 as bv8, 2 as bv8, 3 as bv8]);
    if written.IsFailure() {
      print "write failure\n";
    } else {
      var read := FileIO.ReadBytesFromFile("package-smoke.bin");
      if read.IsFailure() {
        print "read failure\n";
      } else if read.value == [1 as bv8, 2 as bv8, 3 as bv8] {
        print "file ok\n";
      } else {
        print "wrong file bytes\n";
      }
    }
  }
}
