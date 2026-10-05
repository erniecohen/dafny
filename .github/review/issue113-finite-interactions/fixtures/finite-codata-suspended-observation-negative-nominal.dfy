// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"

type Positive = n:int | 0 < n witness 1
codatatype Refined = R(tail:Refined,field:Positive)
newtype RefinedN = Refined witness *
newtype Observe = int -> int witness *
function Bad():RefinedN {
  var demand := ((x:int) => (map i:int | i == x :: 0 := (Bad() as Refined).field)[0]) as Observe;
  var wrong := (0 * demand(0)) as Positive;
  R(Bad() as Refined,wrong) as RefinedN
}
