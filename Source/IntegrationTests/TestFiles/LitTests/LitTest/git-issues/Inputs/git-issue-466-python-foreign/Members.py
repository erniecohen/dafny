# Module: Members
class default__:
    @staticmethod
    def value():
        from importlib import import_module
        MemberPeer = import_module(".MemberPeer", __package__) if __package__ else import_module("MemberPeer")
        return MemberPeer.default__.Base() + 4
    @staticmethod
    def Callback():
        from importlib import import_module
        MemberPeer = import_module(".MemberPeer", __package__) if __package__ else import_module("MemberPeer")
        return MemberPeer.default__.Base()
class ForeignOps:
    @staticmethod
    def call():
        return 6
    @staticmethod
    def other():
        return 5

def ForeignOps_other():
    return ForeignOps.other()
