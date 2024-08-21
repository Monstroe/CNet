namespace CNet_Referrer;

public enum ServiceSendType
{
    ID,
    RoomCode,
    RoomMembers,
    MemberJoined,
    MemberLeft,
    RoomStart,
    RoomClosed,
    Invalid
}

public enum ServiceReceiveType
{
    CreateRoom,
    JoinRoom,
    LeaveRoom,
    StartRoom,
    CloseRoom
}