using Goosetrap.RobloxInterfaces;

namespace Goosetrap.Tests
{
    public class JoinUriTests
    {
        [Theory]
        [InlineData("https://www.roblox.com/games/1537690962/Bee-Swarm-Simulator", 1537690962L)]
        [InlineData("https://www.roblox.com/games/1537690962/", 1537690962L)]
        [InlineData("https://www.roblox.com/game/1537690962", 1537690962L)]
        [InlineData("roblox.com/games/1818", 1818L)]
        [InlineData("https://www.roblox.com/experiences/1537690962", 1537690962L)]
        [InlineData("roblox://placeId=1537690962", 1537690962L)]
        [InlineData("1537690962", 1537690962L)]
        [InlineData("  1537690962  ", 1537690962L)]
        public void Parses_the_place_id_out_of_common_links(string link, long expectedPlaceId)
        {
            var target = JoinUri.Parse(link);

            Assert.NotNull(target);
            Assert.Equal(expectedPlaceId, target!.PlaceId);
            Assert.False(target.IsPrivateServer);
        }

        [Theory]
        [InlineData("https://www.roblox.com/games/1537690962/Bee-Swarm-Simulator?privateServerLinkCode=37335071446288504650105074256319")]
        [InlineData("roblox://placeId=1537690962&accessCode=37335071446288504650105074256319")]
        public void Recognises_private_server_codes(string link)
        {
            var target = JoinUri.Parse(link);

            Assert.NotNull(target);
            Assert.Equal(1537690962L, target!.PlaceId);
            Assert.True(target.IsPrivateServer);
            Assert.Equal("37335071446288504650105074256319", target.PrivateServerLinkCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("https://www.roblox.com/home")]
        [InlineData("not a link")]
        [InlineData("https://www.roblox.com/users/1/profile")]
        public void Returns_null_for_links_without_a_place(string? link)
        {
            Assert.Null(JoinUri.Parse(link));
        }

        [Fact]
        public void Builds_a_public_join_uri()
        {
            var target = new JoinTarget(1537690962L, null);

            string uri = JoinUri.Build("TICKET123", target, 42);

            Assert.StartsWith("roblox-player:1+launchmode:play", uri);
            Assert.Contains("+gameinfo:TICKET123", uri);
            Assert.Contains("+launchtime:", uri);
            Assert.Contains("+browsertrackerid:42", uri);
            // the place launcher url must be escaped, otherwise the client cannot parse the uri
            Assert.Contains("request%3DRequestGame%26placeId%3D1537690962", uri);
            Assert.DoesNotContain("request=RequestGame", uri);
        }

        [Fact]
        public void Builds_a_private_server_join_uri()
        {
            var target = new JoinTarget(1537690962L, "37335071446288504650105074256319");

            string uri = JoinUri.Build("TICKET123", target, 42);

            Assert.Contains("request%3DRequestPrivateGame", uri);
            Assert.Contains("accessCode%3D37335071446288504650105074256319", uri);
        }

        [Fact]
        public void Skips_the_ticket_when_there_is_none()
        {
            string uri = JoinUri.Build(null, new JoinTarget(1818L, null), 1);

            Assert.DoesNotContain("gameinfo", uri);
            Assert.Contains("placeId%3D1818", uri);
        }

        [Fact]
        public void Place_launcher_url_matches_the_embedded_one()
        {
            var target = new JoinTarget(1818L, null);

            string uri = JoinUri.Build("t", target, 1);
            string escaped = Uri.EscapeDataString(JoinUri.BuildPlaceLauncherUrl(target));

            Assert.Contains(escaped, uri);
        }

        [Theory]
        [InlineData("https://www.roblox.com/share?code=abc123def456&type=Server", true)]
        [InlineData("https://www.roblox.com/games/1818", false)]
        public void Detects_share_links(string link, bool expected)
        {
            Assert.Equal(expected, JoinUri.IsShareLink(link));
        }

        [Fact]
        public void Extracts_the_share_code()
        {
            Assert.Equal("abc123def456", JoinUri.GetShareCode("https://www.roblox.com/share?code=abc123def456&type=Server"));
            Assert.Null(JoinUri.GetShareCode("https://www.roblox.com/games/1818"));
        }
    }
}