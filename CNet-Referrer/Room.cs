using CNet;

namespace CNet_Referrer;

public class Room
{
    public int ID { get; set; }
    public List<Client> Members { get; set; }
    public Client Host { get { return Members[0]; } }
    public List<Client> Guests { get { return Members.GetRange(1, Members.Count - 1); } }

    public Room(int id)
    {
        ID = id;
        Members = new List<Client>();
    }

    public List<Client> GetMembersExcept(Client clientToExclude)
    {
        List<Client> membersExcluding = new List<Client>();
        foreach (Client member in Members)
        {
            if (member != clientToExclude)
            {
                membersExcluding.Add(member);
            }
        }
        return membersExcluding;
    }
}