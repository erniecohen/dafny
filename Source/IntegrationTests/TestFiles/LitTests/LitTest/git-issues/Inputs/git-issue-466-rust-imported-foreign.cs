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

namespace Imported {
  public class Native {
    public Native() { }
    public BigInteger Read() { return 31; }
  }
}

namespace RefinedImported { public class Native { public BigInteger Read() { return 47; } } }
namespace RefinedAgain { public class Native { public BigInteger Read() { return 59; } } }
namespace Nested.async { public class Native { public BigInteger Read() { return 71; } } }

namespace Imported { public class Box<T> { public T Echo(T value) { return value; } } }
namespace RefinedImported { public class Box<T> { public T Echo(T value) { return value; } } }
namespace RefinedAgain { public class Box<T> { public T Echo(T value) { return value; } } }
namespace Nested.async { public class Box<T> { public T Echo(T value) { return value; } } }
