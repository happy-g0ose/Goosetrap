using System.ComponentModel;

using Goosetrap.Models;
using Goosetrap.Resources;
using Goosetrap.RobloxInterfaces;

namespace Goosetrap.Tests
{
    public class FastFlagTests
    {
        [Fact]
        public void AllowlistStatus_follows_the_allowlist_state()
        {
            var flag = new FastFlag { Name = FFlagAllowlist.SkyGray, Value = "True", Allowlisted = true };
            Assert.Equal(Strings.Common_Allowlisted, flag.AllowlistStatus);

            flag.Allowlisted = false;
            Assert.Equal(Strings.Common_NotAllowlisted, flag.AllowlistStatus);
        }

        [Fact]
        public void Changing_the_allowlist_state_notifies_the_editor()
        {
            var flag = new FastFlag();
            var changed = new List<string?>();

            ((INotifyPropertyChanged)flag).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            flag.Allowlisted = true;

            Assert.Contains(nameof(FastFlag.Allowlisted), changed);
            Assert.Contains(nameof(FastFlag.AllowlistStatus), changed);
        }

        [Fact]
        public void Assigning_the_same_value_does_not_notify()
        {
            var flag = new FastFlag { Allowlisted = true };
            var notified = false;

            ((INotifyPropertyChanged)flag).PropertyChanged += (_, _) => notified = true;

            flag.Allowlisted = true;

            Assert.False(notified);
        }

        [Theory]
        [InlineData(FFlagAllowlist.SkyGray, true)]
        [InlineData("DFIntTaskSchedulerTargetFps", false)]
        [InlineData("FFlagDebugForceDisableShadows", false)]
        public void Status_matches_the_shared_allowlist(string name, bool expected)
        {
            var flag = new FastFlag { Name = name, Value = "True" };
            flag.Allowlisted = FastFlagManager.IsAllowlisted(flag.Name);

            Assert.Equal(expected, flag.Allowlisted);
            Assert.Equal(expected ? Strings.Common_Allowlisted : Strings.Common_NotAllowlisted, flag.AllowlistStatus);
        }
    }
}