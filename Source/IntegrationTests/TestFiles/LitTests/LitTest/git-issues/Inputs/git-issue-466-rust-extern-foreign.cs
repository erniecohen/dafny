using System.Numerics;
namespace Foreign.Types {
  public partial class Actual {
    public Actual() { }
    public BigInteger Read() { return 13; }
  }
}
namespace Foreign {
  public static class Tools {
    public static A._ITree<BigInteger> Echo(A._ITree<BigInteger> x) { return x; }
    public static BigInteger Identity(BigInteger x) { return x; }
  }
}
namespace NativeOnly {
  public class Opaque { }
  public partial class __default {
    public static BigInteger Zero() { return 0; }
  }
}

namespace Foreign.Types {
  public partial class Implementation {
    public BigInteger ExternalEval() { return 17; }
  }
}
