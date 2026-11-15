class C {
  const flag:bool
  constructor(b:bool) ensures flag==b { flag:=b; }
  ghost predicate F() reads if flag then {this} else {} { !flag }
  ghost method M() requires F() reads {} {}
  ghost method Bad() requires flag reads {} { M(); }
}
