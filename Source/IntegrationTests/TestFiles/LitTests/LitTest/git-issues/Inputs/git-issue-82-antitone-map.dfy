// The image contains every integer n >= 2, with witness y = -n - 1.
ghost function InvalidAntitoneImage(): map<int, int> {
  map x: int, y: int | 0 <= x && -x-x < y && y < 0 && x < -y :: x := 0
}
