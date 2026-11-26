include "git-issue-466-python-foreign-source.dfy"
method Main() {
  var onlyType := OnlyTypePeer.Read();
  var n := TypePeer.Read();
  var m := Members.Call();
  print m, " ";
  var o := OmittedTypePeer.Read();
  print o, " ", OmittedMemberPeer.Read(), " ", Peer.Read(), " ", n, " ", MemberPeer.Read(), " ", OmittedPeer.Read(), " ", AOnlyPeer.Read(), " ", onlyType, "\n";
}
