module JSONControl {
  import opened Std.BoundedInts
  import Std.JSON.Deserializer

  lemma CanonicalHexDigits()
  {
    Deserializer.Uint16StrConversion.CharsConsistent();
    assert Deserializer.Uint16StrConversion.base == 16;
    assert ('A' as uint16) in Deserializer.Uint16StrConversion.charToDigit;
    assert Deserializer.Uint16StrConversion.charToDigit['A' as uint16] == 10;
    // CONTROL
  }

  function LargestFourDigitHex(): uint16 {
    Deserializer.ToNat16(['F' as uint16, 'F' as uint16, 'F' as uint16, 'F' as uint16])
  }
}
