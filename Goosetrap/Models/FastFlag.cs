using System.ComponentModel;

namespace Goosetrap.Models
{
    public class FastFlag : INotifyPropertyChanged
    {
        private bool _allowlisted;

        // public bool Enabled { get; set; }
        public string Name { get; set; } = null!;
        public string Value { get; set; } = null!;

        /// <summary>
        /// Whether Roblox will actually apply this flag, see <see cref="Goosetrap.RobloxInterfaces.FFlagAllowlist"/>.
        /// </summary>
        public bool Allowlisted
        {
            get => _allowlisted;
            set
            {
                if (_allowlisted == value)
                    return;

                _allowlisted = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Allowlisted)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllowlistStatus)));
            }
        }

        public string AllowlistStatus => Allowlisted ? Strings.Common_Allowlisted : Strings.Common_NotAllowlisted;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
