using System.Collections.Generic;

namespace Goosetrap.Models.Persistable
{
    public class AccountEntry
    {
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public long UserId { get; set; }
        public string EncryptedCookie { get; set; } = "";
        public string AvatarUri { get; set; } = "";
    }

    public class AccountsState
    {
        public List<AccountEntry> Accounts { get; set; } = new();
    }
}
