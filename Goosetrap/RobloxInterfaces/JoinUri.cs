namespace Goosetrap.RobloxInterfaces
{
    /// <summary>
    /// Where a client should be sent when it starts.
    /// </summary>
    /// <param name="PlaceId">The place to join.</param>
    /// <param name="PrivateServerLinkCode">The private server code, null for a public server.</param>
    public sealed record JoinTarget(long PlaceId, string? PrivateServerLinkCode)
    {
        public bool IsPrivateServer => !String.IsNullOrEmpty(PrivateServerLinkCode);

        public string DisplayName => IsPrivateServer ? $"place {PlaceId} (private server)" : $"place {PlaceId}";
    }

    /// <summary>
    /// Turns the links users copy out of their browser into a roblox-player launch uri.
    ///
    /// The Roblox client gets its game join information from the "placelauncherurl" parameter of that
    /// uri, and its authentication from the "gameinfo" parameter (the authentication ticket), so a
    /// client that is handed a proper uri authenticates as the account whose ticket was embedded -
    /// that is what makes joining a server with several accounts at once work.
    /// </summary>
    public static class JoinUri
    {
        private const string PlaceLauncherBaseUrl = "https://www.roblox.com/Game/PlaceLauncher.ashx";

        private static readonly Regex PlaceIdPattern = new(
            @"(?:/games/|/game/|placeId[=:]|placeid[=:]|/experiences/|^)(?<id>\d{3,})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex PrivateServerCodePattern = new(
            @"(?:privateServerLinkCode|accessCode|linkCode)[=:](?<code>[0-9a-fA-F-]{6,})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ShareCodePattern = new(
            @"roblox\.com/share\?(?:[^#]*&)?code[=:](?<code>[0-9a-fA-F-]{6,})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses anything the user is likely to paste: a game link, a private server link, a share
        /// link, a roblox:// uri or a bare place id.
        /// </summary>
        /// <returns>null when no place id could be found.</returns>
        public static JoinTarget? Parse(string? input)
        {
            if (String.IsNullOrWhiteSpace(input))
                return null;

            string value = input.Trim().Trim('"', '\'');

            // roblox://placeId=1234&accessCode=... style deep links
            value = value.Replace("roblox://", "placeId=", StringComparison.OrdinalIgnoreCase);

            var placeMatch = PlaceIdPattern.Match(value);

            if (!placeMatch.Success || !long.TryParse(placeMatch.Groups["id"].Value, out long placeId) || placeId <= 0)
                return null;

            var codeMatch = PrivateServerCodePattern.Match(value);
            string? code = codeMatch.Success ? codeMatch.Groups["code"].Value : null;

            return new JoinTarget(placeId, code);
        }

        /// <summary>
        /// True when the link is a share link (roblox.com/share?code=...), which has to be resolved
        /// through the Roblox share links api before it can be joined.
        /// </summary>
        public static bool IsShareLink(string? input) => !String.IsNullOrWhiteSpace(input) && ShareCodePattern.IsMatch(input);

        public static string? GetShareCode(string? input)
        {
            if (String.IsNullOrWhiteSpace(input))
                return null;

            var match = ShareCodePattern.Match(input);
            return match.Success ? match.Groups["code"].Value : null;
        }

        /// <summary>
        /// Builds the launch uri for the Roblox client.
        /// </summary>
        /// <param name="authTicket">Authentication ticket of the account that should be joined, may be empty.</param>
        /// <param name="target">The place/private server to join.</param>
        /// <param name="browserTrackerId">Arbitrary id, Roblox only uses it for telemetry.</param>
        public static string Build(string? authTicket, JoinTarget target, long browserTrackerId)
        {
            string placeLauncherUrl = target.IsPrivateServer
                ? $"{PlaceLauncherBaseUrl}?request=RequestPrivateGame&placeId={target.PlaceId}&accessCode={target.PrivateServerLinkCode}"
                : $"{PlaceLauncherBaseUrl}?request=RequestGame&placeId={target.PlaceId}";

            var sb = new StringBuilder();

            sb.Append("roblox-player:1");
            sb.Append("+launchmode:play");

            if (!String.IsNullOrEmpty(authTicket))
                sb.Append($"+gameinfo:{authTicket}");

            sb.Append($"+launchtime:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            sb.Append($"+placelauncherurl:{Uri.EscapeDataString(placeLauncherUrl)}");
            sb.Append($"+browsertrackerid:{browserTrackerId}");
            sb.Append("+robloxLocale:en_us+gameLocale:en_us+LaunchExp:InApp");

            return sb.ToString();
        }

        /// <summary>
        /// The place launcher url that is embedded in a built uri, exposed for tests and diagnostics.
        /// </summary>
        public static string BuildPlaceLauncherUrl(JoinTarget target) => target.IsPrivateServer
            ? $"{PlaceLauncherBaseUrl}?request=RequestPrivateGame&placeId={target.PlaceId}&accessCode={target.PrivateServerLinkCode}"
            : $"{PlaceLauncherBaseUrl}?request=RequestGame&placeId={target.PlaceId}";
    }
}