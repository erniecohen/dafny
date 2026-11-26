class Empty:
    def ctor__(self):
        pass
class default__:
    @staticmethod
    def Callback():
        from importlib import import_module
        OnlyTypePeer = import_module(".OnlyTypePeer", __package__) if __package__ else import_module("OnlyTypePeer")
        return OnlyTypePeer.default__.Base()
