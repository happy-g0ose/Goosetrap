using System.Text.Json;
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

        private static Dictionary<long, JoinTarget?>? _launchIntent;
        private static readonly object _intentLock = new();

        /// <summary>
        /// What each account was last launched into, used to restart a client that crashed. Persisted to
        /// disk, so a re-join still lands in the right server after the menu was closed and reopened.
        /// </summary>
        public static Dictionary<long, JoinTarget?> LaunchIntent
        {
            get
            {
                if (_launchIntent is not null)
                    return _launchIntent;

                lock (_intentLock)
                {
                    _launchIntent ??= LoadLaunchIntent();
                    return _launchIntent;
                }
            }
        }

        /// <summary>
        /// The join link currently filled in on the Accounts page. Every account launch uses it, and so
        /// does the crash watcher when it restarts a client that died.
        /// </summary>
        public static string JoinLink { get; set; } = "";

        private static string? GetFilePath()
        {
            try
            {
                return String.IsNullOrEmpty(Paths.Base) ? null : Path.Combine(Paths.Base, "LaunchIntent.json");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Writes the current launch intent to disk. Called after every account launch.
        /// </summary>
        public static void SaveLaunchIntent()
        {
            const string LOG_IDENT = "AccountPidRegistry::SaveLaunchIntent";

            try
            {
                string? file = GetFilePath();

                if (file is null)
                    return;

                lock (_intentLock)
                {
                    if (_launchIntent is null)
                        return;

                    var payload = new Dictionary<string, IntentEntry>();

                    foreach (var pair in _launchIntent)
                    {
                        payload[pair.Key.ToString()] = new IntentEntry
                        {
                            PlaceId = pair.Value?.PlaceId ?? 0,
                            PrivateServerLinkCode = pair.Value?.PrivateServerLinkCode
                        };
                    }

                    File.WriteAllText(file, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Could not save where the accounts were launched into: {ex.Message}");
            }
        }

        private static Dictionary<long, JoinTarget?> LoadLaunchIntent()
        {
            const string LOG_IDENT = "AccountPidRegistry::LoadLaunchIntent";

            var result = new Dictionary<long, JoinTarget?>();

            try
            {
                string? file = GetFilePath();

                if (file is null || !File.Exists(file))
                    return result;

                var payload = JsonSerializer.Deserialize<Dictionary<string, IntentEntry>>(File.ReadAllText(file));

                if (payload is null)
                    return result;

                foreach (var pair in payload)
                {
                    if (!long.TryParse(pair.Key, out long userId) || pair.Value is null)
                        continue;

                    // a zero place id means the account was only signed in, never sent to a game
                    result[userId] = pair.Value.PlaceId > 0
                        ? new JoinTarget(pair.Value.PlaceId, pair.Value.PrivateServerLinkCode)
                        : null;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Could not load the saved launch intent: {ex.Message}");
            }

            return result;
        }

        private class IntentEntry
        {
            public long PlaceId { get; set; }
            public string? PrivateServerLinkCode { get; set; }
        }
    }
}