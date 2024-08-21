using CNet;

namespace CNet_Referrer;

public class PacketSender
{
    public static PacketSender Instance { get; } = new PacketSender();

    public void ID(Client client, Guid id)
    {
        using (NetPacket packet = new NetPacket(Referrer.Instance.Listener.System, PacketProtocol.TCP))
        {
            packet.Write((short)ServiceSendType.ID);
            packet.Write(id.ToString());
            Referrer.Instance.Send(client, packet, PacketProtocol.TCP);
        }
    }

    public void RoomCode(Client client, int roomCode)
    {
        using (NetPacket packet = new NetPacket(Referrer.Instance.Listener.System, PacketProtocol.TCP))
        {
            packet.Write((short)ServiceSendType.RoomCode);
            packet.Write(roomCode);
            Referrer.Instance.Send(client, packet, PacketProtocol.TCP);
        }
    }

    public void RoomMembers(Client client, List<Client> members)
    {
        using (NetPacket packet = new NetPacket(Referrer.Instance.Listener.System, PacketProtocol.TCP))
        {
            packet.Write((short)ServiceSendType.RoomMembers);
            packet.Write(members.Count);
            foreach (Client member in members)
            {
                packet.Write(member.ID.ToString());
            }
            Referrer.Instance.Send(client, packet, PacketProtocol.TCP);
        }
    }

    public void MemberJoined(List<Client> clients, Guid memberID)
    {
        using (NetPacket packet = new NetPacket(Referrer.Instance.Listener.System, PacketProtocol.TCP))
        {
            packet.Write((short)ServiceSendType.MemberJoined);
            packet.Write(memberID.ToString());
            Referrer.Instance.Send(clients, packet, PacketProtocol.TCP);
        }
    }

    public void MemberLeft(Client client, Guid memberID)
    {
        using (NetPacket packet = new NetPacket(Referrer.Instance.Listener.System, PacketProtocol.TCP))
        {
            packet.Write((short)ServiceSendType.MemberLeft);
            packet.Write(memberID.ToString());
            Referrer.Instance.Send(client, packet, PacketProtocol.TCP);
        }
    }

    public void RoomStart(List<Client> clients)
    {
        using (NetPacket packet = new NetPacket(Referrer.Instance.Listener.System, PacketProtocol.TCP))
        {
            packet.Write((short)ServiceSendType.RoomStart);
            Referrer.Instance.Send(clients, packet, PacketProtocol.TCP);
        }
    }

    public void RoomClosed(List<Client> clients)
    {
        using (NetPacket packet = new NetPacket(Referrer.Instance.Listener.System, PacketProtocol.TCP))
        {
            packet.Write((short)ServiceSendType.RoomClosed);
            Referrer.Instance.Send(clients, packet, PacketProtocol.TCP);
        }
    }

    public void Invalid(Client client, string errorMessage)
    {
        using (NetPacket packet = new NetPacket(Referrer.Instance.Listener.System, PacketProtocol.TCP))
        {
            packet.Write((short)ServiceSendType.Invalid);
            packet.Write(errorMessage);
            Referrer.Instance.Send(client, packet, PacketProtocol.TCP);
        }
    }
}