using Goosetrap.RobloxInterfaces;

namespace Goosetrap.Utility
{
    /// <summary>
    /// Shared state between the account manager and the client watcher.
    ///
    /// The authentication ticket ends up in the command line of the client that Goosetrap started, which
    /// is how a running client is matched back to the account it was launched with.
    /// </summary>
    public static class AccountPidRegistry
    {
        /// <summary>
        /// ticket → account, so a running client can be shown with the right name even if Roblox
        /// restarts itself with a new pid.
        /// </summary>
        public static readonly Dictionary<string, (string Username, string DisplayName, long UserId)> TicketMap = new();

        /// <summary>
        /// What each account was last launched into, used to restart a client that crashed.
        /// </summary>
        public static readonly Dictionary<long, JoinTarget?> LaunchIntent = new();

        /// <summary>
        /// The join link currently filled in on the Accounts page. Every account launch uses it, and so
        /// does the crash watcher when it restarts a client that died.
        /// </summary>
        public static string JoinLink { get; set; } = "";
    }
}