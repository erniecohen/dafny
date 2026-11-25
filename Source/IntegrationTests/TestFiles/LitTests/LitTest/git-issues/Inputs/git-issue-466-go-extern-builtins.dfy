// Empty extern module names refer to Go's built-in scope. C# has no such module.
module {:extern ""} A {
  import B
  trait {:extern} error extends object { }
}
module B {
  import A
  method Test() {
    var absent: A.error? := null;
    print absent == null, "\n";
  }
}
module Test {
  import B
  method Main() { B.Test(); }
}
