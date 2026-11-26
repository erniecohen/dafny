using System.Numerics;
namespace ForeignB {
  public class Handle {
    public static Handle Make() { return new Handle(); }
    public BigInteger Read() { return 23; }
  }
  public partial class Actual {
    public BigInteger number = 19;
    public BigInteger Increment(BigInteger x) { return x + 1; }
    public BigInteger Read() { return 13; }
  }
}
namespace Foreign {
  public static class Tools {
    public static A._ITree<BigInteger> Echo(A._ITree<BigInteger> x) { return x; }
    public static BigInteger Increment(BigInteger x) { return x + 1; }
    public static BigInteger ExternalEval() { return 17; }
    public static BigInteger Identity(BigInteger x) { return x; }
  }
}
namespace NativeOnly {
  public partial class __default {
    public static BigInteger Zero() { return 0; }
  }
}
