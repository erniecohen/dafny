class Renamed:
    def __init__(self, n):
        self.n = n
    def get(self):
        return self.n + 3
class default__:
    @staticmethod
    def Callback():
        from importlib import import_module
        TypePeer = import_module(".TypePeer", __package__) if __package__ else import_module("TypePeer")
        return TypePeer.default__.Base()

class D_Value:
    def __init__(self, x):
        self.x = x
    def __dafnystr__(self):
        return "Types.D.Value(" + str(self.x) + ")"
