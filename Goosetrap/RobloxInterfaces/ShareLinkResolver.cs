namespace Goosetrap.RobloxInterfaces
{
    /// <summary>
    /// Resolves "roblox.com/share?code=..." links, which is the form the Roblox website and the desktop
    /// app now hand out for private servers. Such a link does not carry the place id or an access code,
    /// so it has to be exchanged for the real join data via the share links api first.
    ///
    /// Note that this is Roblox's own api and it needs the cookie of a signed in account.
    /// </summary>
    public static class ShareLinkResolver
    {
        private const string LOG_IDENT = "ShareLinkResolver";
        private const string ResolveUrl = "https://apis.roblox.com/sharelinks/v1/resolve-link";

        /// <summary>
        /// Exchanges a share code for a joinable target.
        /// </summary>
        /// <returns>null when the link could not be resolved (expired, invalid, no connection).</returns>
        public static async Task<JoinTarget?> ResolveAsync(string shareCode, string cookie)
        {
            if (String.IsNullOrWhiteSpace(shareCode) || String.IsNullOrEmpty(cookie))
                return null;

            try
            {
                string payload = JsonSerializer.Serialize(new { linkId = shareCode, linkType = "Server" });

                // the first request usually comes back with 403 and the csrf token we have to echo,
                // which is how roblox protects their api against cross site posts
                var (body, csrf) = await PostAsync(payload, cookie, null);

                if (body is null && !String.IsNullOrEmpty(csrf))
                    (body, _) = await PostAsync(payload, cookie, csrf);

                if (String.IsNullOrEmpty(body))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not resolve the share link");
                    return null;
                }

                var target = Parse(body);

                if (target is null)
                    App.Logger.WriteLine(LOG_IDENT, "Share link resolved but no place id was found in the response");

                return target;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        private static async Task<(string? Body, string? Csrf)> PostAsync(string payload, string cookie, string? csrf)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, ResolveUrl);

            request.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            if (!String.IsNullOrEmpty(csrf))
                request.Headers.Add("X-CSRF-Token", csrf);

            using var response = await client.SendAsync(request);

            string text = await response.Content.ReadAsStringAsync();

            string? newCsrf = response.Headers.Contains("x-csrf-token")
                ? response.Headers.GetValues("x-csrf-token").FirstOrDefault()
                : null;

            if (!response.IsSuccessStatusCode)
                return (null, newCsrf);

            return (text, newCsrf);
        }

        /// <summary>
        /// Walks the response looking for the place id and the access code, so a shape change on Roblox's
        /// side does not break the feature outright.
        /// </summary>
        internal static JoinTarget? Parse(string json)
        {
            using var document = JsonDocument.Parse(json);

            long? placeId = null;
            string? accessCode = null;
            string? linkCode = null;

            Walk(document.RootElement, ref placeId, ref accessCode, ref linkCode);

            if (placeId is null || placeId <= 0)
                return null;

            // a private server invite carries both: the access code is the one the launcher uses
            string? code = !String.IsNullOrEmpty(accessCode) ? accessCode : linkCode;

            return new JoinTarget(placeId.Value, code);
        }

        private static void Walk(JsonElement element, ref long? placeId, ref string? accessCode, ref string? linkCode)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (placeId is null
                            && property.Name.Equals("placeId", StringComparison.OrdinalIgnoreCase)
                            && property.Value.ValueKind == JsonValueKind.Number
                            && property.Value.TryGetInt64(out long id))
                        {
                            placeId = id;
                        }
                        else if (accessCode is null
                            && property.Name.Equals("accessCode", StringComparison.OrdinalIgnoreCase)
                            && property.Value.ValueKind == JsonValueKind.String)
                        {
                            accessCode = property.Value.GetString();
                        }
                        else if (linkCode is null
                            && property.Name.Equals("linkCode", StringComparison.OrdinalIgnoreCase)
                            && property.Value.ValueKind == JsonValueKind.String)
                        {
                            linkCode = property.Value.GetString();
                        }

                        Walk(property.Value, ref placeId, ref accessCode, ref linkCode);
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        Walk(item, ref placeId, ref accessCode, ref linkCode);

                    break;
            }
        }
    }
}