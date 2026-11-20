using System.Numerics;
namespace Foreign {
  public partial class __default {
    public static BigInteger Plain(BigInteger x) { return x + 1; }
    public static BigInteger Renamed(BigInteger x) { return x + 3; }
  }
  public class Actual {
    public static Actual Make() { return new Actual(); }
    public BigInteger Value() { return 13; }
  }
  public static class Tools {
    public static BigInteger Twice(BigInteger x) { return 2 * x; }
    public static BigInteger Triple(BigInteger x) { return 3 * x; }
  }
}
namespace OtherForeign {
  public partial class __default {
    public static BigInteger Get() { return 19; }
  }
}
namespace NativeZero {
  public partial class __default {
    public static BigInteger Get() { return 23; }
  }
}
// C# keeps the declaring namespace for a class's two-argument extern form.
namespace B {
  public class Actual : Foreign.Actual {
    public new static Actual Make() { return new Actual(); }
  }
}
namespace map {
  public static class Tools {
    public static BigInteger Five() { return 5; }
  }
}
