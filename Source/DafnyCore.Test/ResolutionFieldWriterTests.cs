using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test;

public class ResolutionFieldWriterTests {
  private class Parent {
    private readonly object inherited = new();
    public object Inherited => inherited;
  }

  private sealed class Record : Parent {
    public readonly string Text = "before";
    public readonly int Number = 1;
    public readonly int? Optional = 2;
    public readonly DayOfWeek Kind = DayOfWeek.Monday;
  }

  private readonly struct ValueRecord {
    public readonly int Number;
    public readonly object Reference;
    public ValueRecord(int number, object reference) { Number = number; Reference = reference; }
  }

  private static void Write(object target, string name, object? value) {
    var fields = ResolutionGraphSchema.Fields(target.GetType());
    var index = Array.FindIndex(fields, f => f.Name == name);
    Assert.True(index >= 0);
    ResolutionGraphSchema.FieldWriters(target.GetType())[index](target, value);
  }

  [Fact]
  public void RestoresPrivateInheritedAndReadonlyFieldsWithReferenceIdentity() {
    var record = new Record();
    var reference = new object();
    Write(record, "inherited", reference);
    Write(record, nameof(Record.Text), "after");
    Write(record, nameof(Record.Number), 42);
    Write(record, nameof(Record.Optional), null);
    Write(record, nameof(Record.Kind), DayOfWeek.Friday);
    Assert.Same(reference, record.Inherited);
    Assert.Equal("after", record.Text);
    Assert.Equal(42, record.Number);
    Assert.Null(record.Optional);
    Assert.Equal(DayOfWeek.Friday, record.Kind);
    Write(record, nameof(Record.Optional), 17);
    Assert.Equal(17, record.Optional);
  }

  [Fact]
  public void RestoresTheOriginalValueTypeBox() {
    object value = new ValueRecord(1, new object());
    var reference = new object();
    Write(value, nameof(ValueRecord.Number), 42);
    Write(value, nameof(ValueRecord.Reference), reference);
    Assert.Equal(42, ((ValueRecord)value).Number);
    Assert.Same(reference, ((ValueRecord)value).Reference);
  }

  [Fact]
  public void RejectsAnIncompatibleFieldValue() {
    var record = new Record();
    var error = Assert.ThrowsAny<Exception>(() => Write(record, nameof(Record.Number), "wrong type"));
    Assert.True(error is InvalidCastException or ArgumentException);
    Assert.Equal(1, record.Number);
  }
}
