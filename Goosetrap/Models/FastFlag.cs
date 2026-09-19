namespace Goosetrap.Models
{
    public class FastFlag
    {
        // public bool Enabled { get; set; }
        public string Name { get; set; } = null!;
        public string Value { get; set; } = null!;

        /// <summary>
        /// Whether Roblox will actually apply this flag, see <see cref="Goosetrap.RobloxInterfaces.FFlagAllowlist"/>.
        /// </summary>
        public bool Allowlisted { get; set; }

        public string AllowlistStatus => Allowlisted ? Strings.Common_Allowlisted : Strings.Common_NotAllowlisted;
    }
}
