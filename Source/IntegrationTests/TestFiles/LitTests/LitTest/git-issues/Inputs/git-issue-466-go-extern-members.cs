using System.Numerics;
namespace A {
  public partial class __default {
    public static BigInteger Plain(BigInteger x) { return x + 1; }
    public static BigInteger Renamed(BigInteger x) { return 2 * x; }
    public static B.Box Echo(B.Box x) { return x; }
  }
  public partial class Actual {
    public BigInteger Value() { return 13; }
  }
  public partial class Mixed {
    public BigInteger RenamedInstance() { return 17; }
  }
}
