function SpecConverter(input: string): int { 1 }
type StringWrapper { ghost const base: string }
method Converter(value: StringWrapper) returns (res: int) ensures res == SpecConverter(value.base) { res := 1; assert res == SpecConverter(value.base); }
