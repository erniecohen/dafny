const K0: int := 0x1_0000_0000
const K1: int := 0x2_0000_0000
const K2: int := 0x3_0000_0000
const K3: int := 0x4_0000_0000
const K4: int := 0x5_0000_0000
const K5: int := 0x6_0000_0000
const K6: int := 0x7_0000_0000
const K7: int := 0x8_0000_0000

lemma Ordered() ensures K0 < K1 < K2 < K3 < K4 < K5 < K6 < K7 { }
