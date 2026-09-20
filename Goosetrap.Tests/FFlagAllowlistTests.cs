using Goosetrap.RobloxInterfaces;

namespace Goosetrap.Tests
{
    /// <summary>
    /// The Roblox client only applies the Fast Flags that are on its Fast Flag Allowlist,
    /// everything else written to ClientAppSettings.json is silently ignored.
    /// </summary>
    public class FFlagAllowlistTests
    {
        [Theory]
        [InlineData(FFlagAllowlist.CSGLevelOfDetailSwitchingDistance)]
        [InlineData(FFlagAllowlist.CSGLevelOfDetailSwitchingDistanceL12)]
        [InlineData(FFlagAllowlist.CSGLevelOfDetailSwitchingDistanceL23)]
        [InlineData(FFlagAllowlist.CSGLevelOfDetailSwitchingDistanceL34)]
        [InlineData(FFlagAllowlist.HandleAltEnterFullscreenManually)]
        [InlineData(FFlagAllowlist.TextureQualityOverrideEnabled)]
        [InlineData(FFlagAllowlist.TextureQualityOverride)]
        [InlineData(FFlagAllowlist.ForceMSAASamples)]
        [InlineData(FFlagAllowlist.DisableDPIScale)]
        [InlineData(FFlagAllowlist.GraphicsPreferD3D11)]
        [InlineData(FFlagAllowlist.GraphicsPreferVulkan)]
        [InlineData(FFlagAllowlist.GraphicsPreferOpenGL)]
        [InlineData(FFlagAllowlist.SkyGray)]
        [InlineData(FFlagAllowlist.DebugPauseVoxelizer)]
        [InlineData(FFlagAllowlist.FRMQualityLevelOverride)]
        [InlineData(FFlagAllowlist.FRMMinGrassDistance)]
        [InlineData(FFlagAllowlist.FRMMaxGrassDistance)]
        [InlineData(FFlagAllowlist.GrassMovementReducedMotionFactor)]
        public void Allowlisted_flags_are_recognised(string flag)
        {
            Assert.True(FFlagAllowlist.IsAllowed(flag), $"{flag} should be on the allowlist");
        }

        [Theory]
        [InlineData("DFIntTaskSchedulerTargetFps")]
        [InlineData("FFlagGameRealTimeD3D11DisableVsync")]
        [InlineData("FFlagDebugForceDisableShadows")]
        [InlineData("FFlagDebugForceDisableAntiAliasing")]
        [InlineData("FFlagDebugDisablePostEffects")]
        [InlineData("FIntDebugTextureManagerSkipMips")]
        [InlineData("FFlagDebugForceFutureIsBrightPhase3")]
        [InlineData("")]
        public void Flags_that_are_not_on_the_allowlist_are_rejected(string flag)
        {
            Assert.False(FFlagAllowlist.IsAllowed(flag), $"{flag} is not on the allowlist and must be reported as ignored");
        }

        [Fact]
        public void Allowlist_is_case_sensitive()
        {
            // Roblox itself matches flags case sensitively, so a wrongly cased flag is ignored too
            Assert.False(FFlagAllowlist.IsAllowed(FFlagAllowlist.SkyGray.ToLowerInvariant()));
        }

        [Fact]
        public void Every_entry_looks_like_a_fast_flag()
        {
            Assert.All(FFlagAllowlist.AllowedFlags, flag =>
                Assert.Matches(@"^(F|DF|SF)(Flag|Int|String|Log)[A-Za-z0-9]+$", flag));
        }

        [Fact]
        public void Raw_name_lookup_is_ordinal()
        {
            Assert.True(FFlagAllowlist.AllowedFlags.Contains("FFlagDebugSkyGray"));
            Assert.False(FFlagAllowlist.AllowedFlags.Contains("fflagdebugskygray"));
        }
    }
}