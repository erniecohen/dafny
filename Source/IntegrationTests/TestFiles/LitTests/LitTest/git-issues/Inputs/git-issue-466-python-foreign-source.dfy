module {:extern "ForeignModule"} Foreign {
  import Peer
  function {:extern "foreign_value"} Value(): int
  function Callback(): int { Peer.Base() + 1 }
}
module Peer {
  import Foreign
  function Base(): int { 10 }
  function Read(): int { Foreign.Value() + Foreign.Callback() }
}
module Types {
  import TypePeer
  datatype D = Zero | Value(x: int)
  class {:extern "Renamed"} Foreign {
    constructor {:extern} (n: int)
    function {:extern "get"} Get(): int
  }
  function Callback(): int { TypePeer.Base() }
}
module TypePeer {
  import Types
  function Base(): int { 20 }
  method Read() returns (n: int) {
    var c := new Types.Foreign(Types.Callback());
    n := c.Get();
    var d := Types.D.Value(7);
    print d, " ";
  }
}
module Members {
  import MemberPeer
  function {:extern "value"} Value(): int
  function {:extern "Members.ForeignOps", "other"} Other(): int
  method {:extern "Members.ForeignOps", "call"} Call() returns (n: int)
  function Callback(): int { MemberPeer.Base() }
}
module MemberPeer {
  import Members
  function Base(): int { 30 }
  function Read(): int { Members.Value() + Members.Other() + Members.Callback() }
}
module {:compile false} Omitted {
  import OmittedPeer
  function Value(): int { OmittedPeer.Base() + 1 }
}
module OmittedPeer {
  import Omitted
  function Base(): int { 40 }
  function Read(): int { Omitted.Value() }
}
module OmittedTypes {
  import OmittedTypePeer
  class {:compile false} Hidden {
    constructor () { }
    function Get(): int { 50 }
  }
  function Callback(): int { OmittedTypePeer.Base() }
}
module OmittedTypePeer {
  import OmittedTypes
  function Base(): int { 5 }
  method Read() returns (n: int) {
    var c := new OmittedTypes.Hidden();
    n := c.Get() + OmittedTypes.Callback();
  }
}
module OmittedMembers {
  import OmittedMemberPeer
  function {:compile false} Value(): int { OmittedMemberPeer.Base() + 1 }
  function Callback(): int { OmittedMemberPeer.Base() }
}
module OmittedMemberPeer {
  import OmittedMembers
  function Base(): int { 60 }
  function Read(): int { OmittedMembers.Value() + OmittedMembers.Callback() }
}
// Isolate a module attribute from attributes on its declarations.
module AOnlyPeer {
  import OnlyForeign
  function Base(): int { 7 }
  function Read(): int { OnlyForeign.Value() }
}
module {:extern "OnlyForeignModule"} OnlyForeign {
  import AOnlyPeer
  function Value(): int { AOnlyPeer.Base() + 1 }
}
// Isolate a type attribute from attributes on its members.
module OnlyTypes {
  import OnlyTypePeer
  class {:extern "Empty"} Foreign { constructor () { } }
  function Callback(): int { OnlyTypePeer.Base() }
}
module OnlyTypePeer {
  import OnlyTypes
  function Base(): int { 9 }
  method Read() returns (n: int) {
    var c: OnlyTypes.Foreign? := new OnlyTypes.Foreign();
    n := if c != null then OnlyTypes.Callback() else 0;
  }
}
