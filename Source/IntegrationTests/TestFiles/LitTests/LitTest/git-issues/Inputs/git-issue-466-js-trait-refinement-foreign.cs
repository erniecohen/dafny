using System.Numerics;
namespace ForeignB {
  public class NativeViewer<T> : NativeView, NativeGenericView<T> {
    private T value;
    public NativeViewer(T value) { this.value = value; }
    public static NativeViewer<T> Make(T value) { return new NativeViewer<T>(value); }
    public T Read() { return value; }
    public BigInteger Value() { return 173; }
    public BigInteger RootValue() { return 183; }
  }
}
namespace ForeignRefined {
  public class NativeViewer<T> : NativeView, NativeGenericView<T> {
    private T value;
    public NativeViewer(T value) { this.value = value; }
    public static NativeViewer<T> Make(T value) { return new NativeViewer<T>(value); }
    public T Read() { return value; }
    public BigInteger Value() { return 179; }
    public BigInteger RootValue() { return 189; }
  }
}
namespace ForeignFinal {
  public class NativeViewer<T> : NativeView, NativeGenericView<T> {
    private T value;
    public NativeViewer(T value) { this.value = value; }
    public static NativeViewer<T> Make(T value) { return new NativeViewer<T>(value); }
    public T Read() { return value; }
    public BigInteger Value() { return 181; }
    public BigInteger RootValue() { return 191; }
  }
}
