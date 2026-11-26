using System.Numerics;
namespace ForeignModule {
  public partial class __default {
    public static BigInteger foreign_value() => Peer.__default.Base() + 2;
  }
}
namespace Types {
  public class Renamed {
    private BigInteger n;
    public Renamed(BigInteger n) { this.n = n; }
    public BigInteger get() => n + 3;
  }
}
namespace Members {
  public partial class __default {
    public static BigInteger value() => MemberPeer.__default.Base() + 4;
  }
}
namespace Members {
  public class ForeignOps {
    public static BigInteger other() => 5;
    public static BigInteger call() => 6;
  }
}
namespace Omitted {
  public partial class __default {
    public static BigInteger Value() => OmittedPeer.__default.Base() + 1;
  }
}

namespace OmittedTypes {
  public class Hidden {
    public void __ctor() { }
    public BigInteger Get() => 50;
  }
}
