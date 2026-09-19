using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;

using Wpf.Ui.Mvvm.Contracts;

using Goosetrap.UI.Elements.Dialogs;
using Goosetrap.RobloxInterfaces;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

namespace Goosetrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for FastFlagEditorPage.xaml
    /// </summary>
    public partial class FastFlagEditorPage
    {
        // believe me when i say there is absolutely zero point to using mvvm for this
        // using a datagrid is a codebehind thing only and thats it theres literally no way around it

        private readonly ObservableCollection<FastFlag> _fastFlagList = new();

        private bool _showPresets = false;
        private string _searchFilter = "";

        public FastFlagEditorPage()
        {
            InitializeComponent();
        }

        private void ReloadList()
        {
            var selectedEntry = DataGrid.SelectedItem as FastFlag;

            _fastFlagList.Clear();

            var presetFlags = FastFlagManager.PresetFlags.Values;

            foreach (var pair in App.FastFlags.Prop.OrderBy(x => x.Key))
            {
                if (!_showPresets && presetFlags.Contains(pair.Key))
                    continue;

                if (!pair.Key.ToLower().Contains(_searchFilter.ToLower()))
                    continue;

                var entry = new FastFlag
                {
                    // Enabled = true,
                    Name = pair.Key,
                    Value = pair.Value.ToString()!,
                    Allowlisted = FastFlagManager.IsAllowlisted(pair.Key)
                };

                /* if (entry.Name.StartsWith("Disable"))
                {
                    entry.Enabled = false;
                    entry.Name = entry.Name[7..];
                } */

                _fastFlagList.Add(entry);
            }

            if (DataGrid.ItemsSource is null)
                DataGrid.ItemsSource = _fastFlagList;

            if (selectedEntry is null)
                return;

            var newSelectedEntry = _fastFlagList.Where(x => x.Name == selectedEntry.Name).FirstOrDefault();

            if (newSelectedEntry is null)
                return;
            
            DataGrid.SelectedItem = newSelectedEntry;
            DataGrid.ScrollIntoView(newSelectedEntry);
        }

        private void ClearSearch(bool refresh = true)
        {
            SearchTextBox.Text = "";
            _searchFilter = "";

            if (refresh)
                ReloadList();
        }

        private void ShowAddDialog()
        {
            var dialog = new AddFastFlagDialog();
            dialog.ShowDialog();

            if (dialog.Result != MessageBoxResult.OK)
                return;

            if (dialog.Tabs.SelectedIndex == 0)
                AddSingle(dialog.FlagNameTextBox.Text.Trim(), dialog.FlagValueTextBox.Text);
            else if (dialog.Tabs.SelectedIndex == 1)
                ImportJSON(dialog.JsonTextBox.Text);
        }

        private void AddSingle(string name, string value)
        {
            FastFlag? entry;

            if (App.FastFlags.GetValue(name) is null)
            {
                entry = new FastFlag
                {
                    // Enabled = true,
                    Name = name,
                    Value = value,
                    Allowlisted = FastFlagManager.IsAllowlisted(name)
                };

                if (!name.Contains(_searchFilter))
                    ClearSearch();

                _fastFlagList.Add(entry);

                App.FastFlags.SetValue(entry.Name, entry.Value);
            }
            else
            {
                Frontend.ShowMessageBox(Strings.Menu_FastFlagEditor_AlreadyExists, MessageBoxImage.Information);

                bool refresh = false;

                if (!_showPresets && FastFlagManager.PresetFlags.Values.Contains(name))
                {
                    TogglePresetsButton.IsChecked = true;
                    _showPresets = true;
                    refresh = true;
                }

                if (!name.Contains(_searchFilter))
                {
                    ClearSearch(false);
                    refresh = true;
                }

                if (refresh)
                    ReloadList();

                entry = _fastFlagList.Where(x => x.Name == name).FirstOrDefault();
            }

            DataGrid.SelectedItem = entry;
            DataGrid.ScrollIntoView(entry);
        }

        private void ImportJSON(string json)
        {
            Dictionary<string, object>? list = null;

            json = json.Trim();

            // autocorrect where possible
            if (!json.StartsWith('{'))
                json = '{' + json;

            if (!json.EndsWith('}'))
            {
                int lastIndex = json.LastIndexOf('}');

                if (lastIndex == -1)
                    json += '}';
                else
                    json = json.Substring(0, lastIndex+1);
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                list = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);

                if (list is null)
                    throw new Exception("JSON deserialization returned null");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(                    
                    String.Format(Strings.Menu_FastFlagEditor_InvalidJSON, ex.Message),
                    MessageBoxImage.Error
                );

                ShowAddDialog();

                return;
            }

            if (list.Count > 16)
            {
                var result = Frontend.ShowMessageBox(
                    Strings.Menu_FastFlagEditor_LargeConfig, 
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                    return;
            }

            var conflictingFlags = App.FastFlags.Prop.Where(x => list.ContainsKey(x.Key)).Select(x => x.Key);
            bool overwriteConflicting = false;

            if (conflictingFlags.Any())
            {
                int count = conflictingFlags.Count();

                string message = String.Format(
                    Strings.Menu_FastFlagEditor_ConflictingImport,
                    count,
                    String.Join(", ", conflictingFlags.Take(25))
                );

                if (count > 25)
                    message += "...";

                var result = Frontend.ShowMessageBox(message, MessageBoxImage.Question, MessageBoxButton.YesNo);

                overwriteConflicting = result == MessageBoxResult.Yes;
            }

            foreach (var pair in list)
            {
                if (App.FastFlags.Prop.ContainsKey(pair.Key) && !overwriteConflicting)
                    continue;

                if (pair.Value is null)
                    continue;

                var val = pair.Value.ToString();

                if (val is null)
                    continue;

                App.FastFlags.SetValue(pair.Key, pair.Value);
            }

            ClearSearch();
        }

        // refresh list on page load to synchronize with preset page
        private void Page_Loaded(object sender, RoutedEventArgs e) => ReloadList();

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.DataContext is not FastFlag entry)
                return;

            if (e.EditingElement is not TextBox textbox)
                return;

            switch (e.Column.Header)
            {
                case "Name":
                    string oldName = entry.Name;
                    string newName = textbox.Text;

                    if (newName == oldName)
                        return;

                    if (App.FastFlags.GetValue(newName) is not null)
                    {
                        Frontend.ShowMessageBox(Strings.Menu_FastFlagEditor_AlreadyExists, MessageBoxImage.Information);
                        e.Cancel = true;
                        textbox.Text = oldName;
                        return;
                    }

                    App.FastFlags.SetValue(oldName, null);
                    App.FastFlags.SetValue(newName, entry.Value);

                    if (!newName.Contains(_searchFilter))
                        ClearSearch();

                    entry.Name = newName;

                    break;

                case "Value":
                    string oldValue = entry.Value;
                    string newValue = textbox.Text;

                    App.FastFlags.SetValue(entry.Name, newValue);

                    break;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
                window.Navigate(typeof(FastFlagsPage));
        }

        private void AddButton_Click(object sender, RoutedEventArgs e) => ShowAddDialog();

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var tempList = new List<FastFlag>();

            foreach (FastFlag entry in DataGrid.SelectedItems)
                tempList.Add(entry);

            foreach (FastFlag entry in tempList)
            {
                _fastFlagList.Remove(entry);
                App.FastFlags.SetValue(entry.Name, null);
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton button)
                return;

            _showPresets = button.IsChecked ?? false;
            ReloadList();
        }

        private void ExportJSONButton_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> FFlagsForExporting = new Dictionary<string, object>();

            var IncludePresetsDialog = Frontend.ShowMessageBox(
                Strings.Menu_FastFlagEditor_ExportJson_IncludePresets,
                MessageBoxImage.Question, 
                MessageBoxButton.YesNo
                );

            foreach (var Flag in App.FastFlags.Prop)
            {
                if (App.FastFlags.IsPreset(Flag.Key) && IncludePresetsDialog != MessageBoxResult.Yes) 
                    continue;

                FFlagsForExporting.Add(Flag.Key, Flag.Value);
            }

            string json = JsonSerializer.Serialize(FFlagsForExporting, new JsonSerializerOptions { WriteIndented = true });
            Clipboard.SetDataObject(json);
            Frontend.ShowMessageBox(Strings.Menu_FastFlagEditor_JsonCopiedToClipboard, MessageBoxImage.Information);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textbox)
                return;

            _searchFilter = textbox.Text;
            ReloadList();
        }

        private void ApplyPresetDictionary(Dictionary<string, object> flags, string presetName)
        {
            var result = Frontend.ShowMessageBox(
                $"Применить конфигурацию «{presetName}»?\nЭто обновит соответствующие значения FastFlag.",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            foreach (var pair in flags)
            {
                App.FastFlags.SetValue(pair.Key, pair.Value);
            }

            ClearSearch();
            Frontend.ShowMessageBox($"Конфигурация «{presetName}» успешно применена!", MessageBoxImage.Information);
        }

        private void PresetPotato_Click(object sender, RoutedEventArgs e)
        {
            // the flags marked as allowlisted are the ones the current Roblox client still applies,
            // everything else is used to be handled by older clients before Roblox introduced the
            // Fast Flag Allowlist and is kept around in case they get allowlisted again
            // https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569
            var potatoFlags = new Dictionary<string, object>
            {
                // framerate
                { "DFIntTaskSchedulerTargetFps", "9999" },
                { "FFlagGameRealTimeD3D11DisableVsync", "True" },

                // textures, mesh detail and grass (allowlisted)
                { FFlagAllowlist.TextureQualityOverrideEnabled, "True" },
                { FFlagAllowlist.TextureQualityOverride, "0" },
                { FFlagAllowlist.ForceMSAASamples, "1" },
                { FFlagAllowlist.CSGLevelOfDetailSwitchingDistance, "0" },
                { FFlagAllowlist.CSGLevelOfDetailSwitchingDistanceL12, "0" },
                { FFlagAllowlist.CSGLevelOfDetailSwitchingDistanceL23, "0" },
                { FFlagAllowlist.CSGLevelOfDetailSwitchingDistanceL34, "0" },
                { FFlagAllowlist.FRMMinGrassDistance, "0" },
                { FFlagAllowlist.FRMMaxGrassDistance, "0" },
                { FFlagAllowlist.GrassMovementReducedMotionFactor, "0" },
                { FFlagAllowlist.SkyGray, "True" },

                // shadows, anti-aliasing and post processing
                { "FFlagDebugForceDisableShadows", "True" },
                { "FFlagDebugForceDisableAntiAliasing", "True" },
                { "FFlagDebugDisablePostEffects", "True" },
                { "FFlagDebugForceFutureIsBrightPhase3", "False" }
            };

            ApplyPresetDictionary(potatoFlags, "Potato PC (Макс FPS)");
        }

        private void PresetUltra_Click(object sender, RoutedEventArgs e)
        {
            var ultraFlags = new Dictionary<string, object>
            {
                // framerate
                { "DFIntTaskSchedulerTargetFps", "9999" },
                { "FFlagGameRealTimeD3D11DisableVsync", "True" },

                // textures, mesh detail and sky (allowlisted)
                { FFlagAllowlist.TextureQualityOverrideEnabled, "True" },
                { FFlagAllowlist.TextureQualityOverride, "3" },
                { FFlagAllowlist.ForceMSAASamples, "4" },
                { FFlagAllowlist.FRMQualityLevelOverride, "21" },
                { FFlagAllowlist.SkyGray, "False" },

                // shadows, anti-aliasing and post processing
                { "FFlagDebugForceDisableShadows", "False" },
                { "FFlagDebugForceDisableAntiAliasing", "False" },
                { "FFlagDebugDisablePostEffects", "False" },
                { "FFlagDebugForceFutureIsBrightPhase3", "True" }
            };

            ApplyPresetDictionary(ultraFlags, "Ultra Graphics");
        }

        private void PresetBalanced_Click(object sender, RoutedEventArgs e)
        {
            var balancedFlags = new Dictionary<string, object>
            {
                // framerate
                { "DFIntTaskSchedulerTargetFps", "9999" },
                { "FFlagGameRealTimeD3D11DisableVsync", "True" },

                // textures and anti-aliasing (allowlisted)
                { FFlagAllowlist.TextureQualityOverrideEnabled, "True" },
                { FFlagAllowlist.TextureQualityOverride, "2" },
                { FFlagAllowlist.ForceMSAASamples, "1" },
                { FFlagAllowlist.SkyGray, "False" },

                // post processing
                { "FFlagDebugDisablePostEffects", "True" },
                { "FFlagDisableInGameMenuBlur", "True" }
            };

            ApplyPresetDictionary(balancedFlags, "Balanced");
        }

        private void PresetReset_Click(object sender, RoutedEventArgs e)
        {
            var result = Frontend.ShowMessageBox(
                "Сбросить все пользовательские FastFlags к стандартным настройкам?",
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            var keysToRemove = App.FastFlags.Prop.Keys.ToList();
            foreach (var key in keysToRemove)
            {
                App.FastFlags.SetValue(key, null);
            }

            ClearSearch();
            Frontend.ShowMessageBox("Все кастомные флаги сброшены!", MessageBoxImage.Information);
        }
    }
}
