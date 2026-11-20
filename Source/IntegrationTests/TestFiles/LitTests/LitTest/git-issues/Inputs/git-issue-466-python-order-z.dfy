module Alpha {
  import Zulu
  datatype T = T(t: Zulu.T)
  const Middle: int := Zulu.End + 1
  function G(n: nat): nat { if n == 0 then 0 else 1 + Zulu.F(n - 1) }
}
