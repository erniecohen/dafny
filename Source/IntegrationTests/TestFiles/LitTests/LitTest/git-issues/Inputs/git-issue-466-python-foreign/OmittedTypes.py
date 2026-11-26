# Module: OmittedTypes
class Hidden:
    def __init__(self):
        pass
    def ctor__(self):
        pass
    def Get(self):
        return 50
class default__:
    @staticmethod
    def Callback():
        from importlib import import_module
        OmittedTypePeer = import_module(".OmittedTypePeer", __package__) if __package__ else import_module("OmittedTypePeer")
        return OmittedTypePeer.default__.Base()
