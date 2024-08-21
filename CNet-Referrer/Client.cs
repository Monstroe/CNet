using CNet;

namespace CNet_Referrer;

public class Client
{
    public Guid ID { get; }
    public NetEndPoint RemoteEP { get; }
    public Room CurrentRoom { get; set; }
    public bool IsHost { get { return CurrentRoom != null && CurrentRoom.Host == this; } }

    public Client(Guid id, NetEndPoint remoteEP)
    {
        ID = id;
        RemoteEP = remoteEP;
    }
}