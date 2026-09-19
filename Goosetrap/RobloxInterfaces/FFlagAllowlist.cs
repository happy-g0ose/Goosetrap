namespace Goosetrap.RobloxInterfaces
{
    /// <summary>
    /// The list of Fast Flags that the Roblox client currently accepts from ClientAppSettings.json.
    ///
    /// Roblox introduced a "Fast Flag Allowlist" on September 29, 2025. Only the flags listed below are
    /// recognised by the current client - every other flag we write into ClientAppSettings.json is silently
    /// ignored and has no effect whatsoever.
    ///
    /// https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569
    ///
    /// The allowlist is subject to change at any time without prior warning, so it has to be kept updated
    /// whenever Roblox adds or removes flags from it.
    /// </summary>
    public static class FFlagAllowlist
    {
        // Geometry
        public const string CSGLevelOfDetailSwitchingDistance = "DFIntCSGLevelOfDetailSwitchingDistance";
        public const string CSGLevelOfDetailSwitchingDistanceL12 = "DFIntCSGLevelOfDetailSwitchingDistanceL12";
        public const string CSGLevelOfDetailSwitchingDistanceL23 = "DFIntCSGLevelOfDetailSwitchingDistanceL23";
        public const string CSGLevelOfDetailSwitchingDistanceL34 = "DFIntCSGLevelOfDetailSwitchingDistanceL34";

        // Rendering
        public const string HandleAltEnterFullscreenManually = "FFlagHandleAltEnterFullscreenManually";
        public const string TextureQualityOverrideEnabled = "DFFlagTextureQualityOverrideEnabled";
        public const string TextureQualityOverride = "DFIntTextureQualityOverride";
        public const string ForceMSAASamples = "FIntDebugForceMSAASamples";
        public const string DisableDPIScale = "DFFlagDisableDPIScale";
        public const string GraphicsPreferD3D11 = "FFlagDebugGraphicsPreferD3D11";
        public const string GraphicsPreferVulkan = "FFlagDebugGraphicsPreferVulkan";
        public const string GraphicsPreferOpenGL = "FFlagDebugGraphicsPreferOpenGL";
        public const string SkyGray = "FFlagDebugSkyGray";
        public const string DebugPauseVoxelizer = "DFFlagDebugPauseVoxelizer";
        public const string FRMQualityLevelOverride = "DFIntDebugFRMQualityLevelOverride";
        public const string FRMMinGrassDistance = "FIntFRMMinGrassDistance";
        public const string FRMMaxGrassDistance = "FIntFRMMaxGrassDistance";

        // User Interface
        public const string GrassMovementReducedMotionFactor = "FIntGrassMovementReducedMotionFactor";

        /// <summary>
        /// Every Fast Flag that the current Roblox client accepts from ClientAppSettings.json.
        /// </summary>
        public static readonly IReadOnlySet<string> AllowedFlags = new HashSet<string>(StringComparer.Ordinal)
        {
            CSGLevelOfDetailSwitchingDistance,
            CSGLevelOfDetailSwitchingDistanceL12,
            CSGLevelOfDetailSwitchingDistanceL23,
            CSGLevelOfDetailSwitchingDistanceL34,

            HandleAltEnterFullscreenManually,
            TextureQualityOverrideEnabled,
            TextureQualityOverride,
            ForceMSAASamples,
            DisableDPIScale,
            GraphicsPreferD3D11,
            GraphicsPreferVulkan,
            GraphicsPreferOpenGL,
            SkyGray,
            DebugPauseVoxelizer,
            FRMQualityLevelOverride,
            FRMMinGrassDistance,
            FRMMaxGrassDistance,

            GrassMovementReducedMotionFactor
        };

        /// <summary>
        /// Determines whether the Roblox client will actually apply the given Fast Flag.
        /// Flags that are not on the allowlist are ignored by the client.
        /// </summary>
        public static bool IsAllowed(string flag) => AllowedFlags.Contains(flag);
    }
}