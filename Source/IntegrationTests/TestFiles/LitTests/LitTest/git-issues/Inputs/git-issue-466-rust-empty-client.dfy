include "git-issue-466-rust-client.dfy"
module EmptyClient {
  import Client
  import EmptyA
  import EmptyRefined
  method Main() {
    Client.Main();
    EmptyA.Arrays(0);
    EmptyRefined.Arrays(0);
  }
}
