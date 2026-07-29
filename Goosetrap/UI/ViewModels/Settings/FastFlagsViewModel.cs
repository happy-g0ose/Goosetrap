using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Goosetrap.Enums.FlagPresets;
using System.Windows;
using Goosetrap.UI.Elements.Settings.Pages;
using Wpf.Ui.Mvvm.Contracts;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Goosetrap.UI.ViewModels.Settings
{
    public class FastFlagsViewModel : NotifyPropertyChangedViewModel
    {
        private Dictionary<string, object>? _preResetFlags;

        public event EventHandler? RequestPageReloadEvent;
        
        public event EventHandler? OpenFlagEditorEvent;

        private void OpenFastFlagEditor() => OpenFlagEditorEvent?.Invoke(this, EventArgs.Empty);

        public ICommand OpenFastFlagEditorCommand => new RelayCommand(OpenFastFlagEditor);

        public bool UseFastFlagManager
        {
            get => App.Settings.Prop.UseFastFlagManager;
            set => App.Settings.Prop.UseFastFlagManager = value;
        }

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA")).Key;
            set => App.FastFlags.SetPreset("Rendering.MSAA", MSAALevels[value]);
        }

        public IReadOnlyDictionary<RenderingMode, string> RenderingModes => FastFlagManager.RenderingModes;

        public RenderingMode SelectedRenderingMode
        {
            get => App.FastFlags.GetPresetEnum(RenderingModes, "Rendering.Mode", "True");
            set
            {
                RenderingMode[] DisableD3D11 = new RenderingMode[]
                {
                    RenderingMode.Vulkan,
                    RenderingMode.OpenGL
                };

                App.FastFlags.SetPresetEnum("Rendering.Mode", value.ToString(), "True");
                App.FastFlags.SetPreset("Rendering.Mode.DisableD3D11", DisableD3D11.Contains(value) ? "True" : null);
            }
        }

        public bool FixDisplayScaling
        {
            get => App.FastFlags.GetPreset("Rendering.DisableScaling") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisableScaling", value ? "True" : null);
        }

        public IReadOnlyDictionary<TextureQuality, string?> TextureQualities => FastFlagManager.TextureQualityLevels;

        public TextureQuality SelectedTextureQuality
        {
            get => TextureQualities.Where(x => x.Value == App.FastFlags.GetPreset("Rendering.TextureQuality.Level")).FirstOrDefault().Key;
            set
            {
                if (value == TextureQuality.Default)
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality.OverrideEnabled", "True");
                    App.FastFlags.SetPreset("Rendering.TextureQuality.Level", TextureQualities[value]);
                }
            }
        }

        private static readonly string[] LODLevels = { "L0", "L12", "L23", "L34" };

        public bool FRMQualityOverrideEnabled
        {
            get => App.FastFlags.GetPreset("Rendering.FRMQualityOverride") != null;
            set
            {
                if (value)
                    FRMQualityOverride = 21;
                else
                    App.FastFlags.SetPreset("Rendering.FRMQualityOverride", null);

                OnPropertyChanged(nameof(FRMQualityOverride));
                OnPropertyChanged(nameof(FRMQualityOverrideEnabled));
            }
        }

        public int FRMQualityOverride
        {
            get => int.TryParse(App.FastFlags.GetPreset("Rendering.FRMQualityOverride"), out var x) ? x : 21;
            set
            {
                App.FastFlags.SetPreset("Rendering.FRMQualityOverride", value);

                OnPropertyChanged(nameof(FRMQualityOverride));
            }
        }

        public bool MeshQualityEnabled
        {
            get => App.FastFlags.GetPreset("Geometry.MeshLOD.Static") != null;
            set
            {
                if (value)
                {
                    // we enable level 3 by default
                    MeshQuality = 3;
                }
                else
                {
                    foreach (string level in LODLevels)
                        App.FastFlags.SetPreset($"Geometry.MeshLOD.{level}", null);

                    App.FastFlags.SetPreset("Geometry.MeshLOD.Static", null);
                }

                OnPropertyChanged(nameof(MeshQualityEnabled));
            }
        }

        public int MeshQuality
        {
            get => int.TryParse(App.FastFlags.GetPreset("Geometry.MeshLOD.Static"), out var x) ? x : 0;
            set
            {
                int clamped = Math.Clamp(value, 0, LODLevels.Length - 1);

                for (int i = 0; i < LODLevels.Length; i++)
                {
                    int lodValue = Math.Clamp(clamped - i, 0, 3);
                    string lodLevel = LODLevels[i];

                    App.FastFlags.SetPreset($"Geometry.MeshLOD.{lodLevel}", lodValue);
                }

                App.FastFlags.SetPreset("Geometry.MeshLOD.Static", clamped);
                OnPropertyChanged(nameof(MeshQuality));
                OnPropertyChanged(nameof(MeshQualityEnabled));
            }
        }

        public string FramerateCap
        {
            get => App.GlobalSettings.GetPreset("Rendering.FramerateCap")!;
            set
            {
                App.GlobalSettings.SetPreset("Rendering.FramerateCap", value);
                OnPropertyChanged(nameof(FramerateCap));
            }
        }

        public bool PotatoGraphicsEnabled
        {
            get => App.FastFlags.GetValue("FIntDebugTextureManagerSkipMips") == "4";
            set
            {
                if (value)
                {
                    App.FastFlags.SetValue("FIntDebugTextureManagerSkipMips", "4");
                    App.FastFlags.SetValue("DFFlagTextureQualityOverrideEnabled", "True");
                    App.FastFlags.SetValue("DFIntTextureQualityOverride", "0");
                    App.FastFlags.SetValue("FIntFRMMinGrassDistance", "0");
                    App.FastFlags.SetValue("FIntFRMMaxGrassDistance", "0");
                    App.FastFlags.SetValue("FIntRenderGrassDetailStrands", "0");
                    App.FastFlags.SetValue("FIntRenderGrassHeightScaler", "0");
                    App.FastFlags.SetValue("FFlagDebugForceLowDetailLevel", "True");
                    App.FastFlags.SetValue("FFlagDebugForceDisableShadows", "True");
                    App.FastFlags.SetValue("FFlagDebugForceDisableAntiAliasing", "True");
                    App.FastFlags.SetValue("FFlagDebugDisablePostEffects", "True");
                    App.FastFlags.SetValue("FFlagDebugDisablePostEffects2", "True");
                    App.FastFlags.SetValue("FFlagDebugForceDisableMultisample", "True");
                }
                else
                {
                    App.FastFlags.SetValue("FIntDebugTextureManagerSkipMips", null);
                    App.FastFlags.SetValue("DFFlagTextureQualityOverrideEnabled", null);
                    App.FastFlags.SetValue("DFIntTextureQualityOverride", null);
                    App.FastFlags.SetValue("FIntFRMMinGrassDistance", null);
                    App.FastFlags.SetValue("FIntFRMMaxGrassDistance", null);
                    App.FastFlags.SetValue("FIntRenderGrassDetailStrands", null);
                    App.FastFlags.SetValue("FIntRenderGrassHeightScaler", null);
                    App.FastFlags.SetValue("FFlagDebugForceLowDetailLevel", null);
                    App.FastFlags.SetValue("FFlagDebugForceDisableShadows", null);
                    App.FastFlags.SetValue("FFlagDebugForceDisableAntiAliasing", null);
                    App.FastFlags.SetValue("FFlagDebugDisablePostEffects", null);
                    App.FastFlags.SetValue("FFlagDebugDisablePostEffects2", null);
                    App.FastFlags.SetValue("FFlagDebugForceDisableMultisample", null);
                }
                OnPropertyChanged(nameof(PotatoGraphicsEnabled));
            }
        }

        public bool ResetConfiguration
        {
            get => _preResetFlags is not null;

            set
            {
                if (value)
                {
                    _preResetFlags = new(App.FastFlags.Prop);
                    App.FastFlags.Prop.Clear();
                }
                else
                {
                    App.FastFlags.Prop = _preResetFlags!;
                    _preResetFlags = null;
                }

                RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
