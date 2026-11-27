# Module: OmittedMembers
class default__:
    @staticmethod
    def Value():
        from importlib import import_module
        OmittedMemberPeer = import_module(".OmittedMemberPeer", __package__) if __package__ else import_module("OmittedMemberPeer")
        return OmittedMemberPeer.default__.Base() + 1
    @staticmethod
    def Callback():
        from importlib import import_module
        OmittedMemberPeer = import_module(".OmittedMemberPeer", __package__) if __package__ else import_module("OmittedMemberPeer")
        return OmittedMemberPeer.default__.Base()
