lemma BadTrigger(e: seq<int>) {
  var m := map i: int {:trigger e[missing]} | 0 <= i < |e| :: i + 1 := e[i];
}
