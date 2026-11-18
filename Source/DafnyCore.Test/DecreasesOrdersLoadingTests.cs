using Microsoft.Dafny;

namespace DafnyCore.Test;

public class DecreasesOrdersLoadingTests {
  [Theory]
  [InlineData(false, false, true)]
  [InlineData(true, false, false)]
  [InlineData(true, true, true)]
  public void LegacyOptOutPreservesOrderLibrarySelection(bool legacy, bool standardLibrary, bool expected) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Datatype);
    options.Set(CommonOptionBag.DecreasesOrders, true);
    options.Set(CommonOptionBag.LegacyNoModularTermination, legacy);
    options.Set(CommonOptionBag.UseStandardLibraries, standardLibrary);
    Assert.Equal(expected, DecreasesOrders.LoadsOrdersLibrary(options));
  }
}
