using System.Numerics;
namespace Foreign {
  public static class Tools {
    public static BigInteger Identity(BigInteger x) { return x; }
  }
}
namespace A {
  public partial class Actual { public BigInteger Value() { return 13; } }
}
namespace Refined {
  public partial class Actual { public BigInteger Value() { return 13; } }
}
