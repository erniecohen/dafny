// There are infinitely many map<bool, int> keys, despite their finite domains.
ghost function InvalidMapCarrierKeys(): map<map<bool, int>, bool> {
  map m: map<bool, int> | true :: true
}
