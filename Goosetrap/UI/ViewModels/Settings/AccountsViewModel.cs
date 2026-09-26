using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Goosetrap.Models.Persistable;
using Goosetrap.RobloxInterfaces;
using Goosetrap.UI.Elements.Dialogs;
using Goosetrap.Utility;

namespace Goosetrap.UI.ViewModels.Settings
{
    public class AccountUIModel : NotifyPropertyChangedViewModel
    {
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public long UserId { get; set; }
        public string AvatarUri { get; set; } = "";
        
        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        private string _ramUsage = "";
        public string RamUsage
        {
            get => _ramUsage;
            set
            {
                if (_ramUsage != value)
                {
                    _ramUsage = value;
                    OnPropertyChanged(nameof(RamUsage));
                }
            }
        }

        public string StatusText => IsRunning ? Strings.Menu_Accounts_Status_Running : Strings.Menu_Accounts_Status_Stopped;
        public string StatusColor => IsRunning ? "#8AE639" : "#808080";

        public ICommand LaunchCommand => new AsyncRelayCommand(LaunchAsync);
        public ICommand OpenProfileCommand => new RelayCommand(OpenProfile);
        public ICommand DeleteCommand { get; set; } = null!;
        public ICommand EditCommand { get; set; } = null!;

        private async Task LaunchAsync()
        {
            const string LOG_IDENT = "AccountUIModel::Launch";
            try
            {
                var entry = App.Accounts.Prop.Accounts.FirstOrDefault(x => x.UserId == UserId);
                if (entry == null) return;

                var target = JoinUri.Parse(AccountPidRegistry.JoinLink);

                bool started = await AccountsHelper.LaunchAccountAsync(entry, target: target);

                if (!started)
                    Frontend.ShowMessageBox(Strings.Menu_Accounts_TicketError, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox(string.Format(Strings.Menu_Accounts_LaunchError, ex.Message), MessageBoxImage.Error);
            }
        }

        private void OpenProfile()
        {
            try
            {
                Process.Start(new ProcessStartInfo($"https://www.roblox.com/users/{UserId}/profile")
                {
                    UseShellExecute = true
                });
            }
            catch {}
        }
    }

    public class AccountsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly DispatcherTimer _timer;

        public ObservableCollection<AccountUIModel> Accounts { get; } = new();

        public ICommand AddAccountCommand => new RelayCommand(AddAccount);

        /// <summary>
        /// Game or private server link the selected accounts should be launched into. Empty means the
        /// clients are only started and signed in.
        /// </summary>
        public string JoinLink
        {
            get => AccountPidRegistry.JoinLink;
            set
            {
                if (AccountPidRegistry.JoinLink == value)
                    return;

                AccountPidRegistry.JoinLink = value ?? "";
                OnPropertyChanged(nameof(JoinLink));
            }
        }

        public ICommand JoinSelectedCommand => new AsyncRelayCommand(JoinSelectedAsync);

        /// <summary>
        /// Restart clients that were started from here and then closed unexpectedly.
        /// </summary>
        public bool AutoRestartCrashedClients
        {
            get => App.Settings.Prop.AutoRestartCrashedClients;
            set
            {
                if (App.Settings.Prop.AutoRestartCrashedClients == value)
                    return;

                App.Settings.Prop.AutoRestartCrashedClients = value;
                App.Settings.Save();
                OnPropertyChanged(nameof(AutoRestartCrashedClients));
            }
        }

        public ICommand SelectAllCommand => new RelayCommand(() => SetSelection(true));

        public ICommand UnselectAllCommand => new RelayCommand(() => SetSelection(false));

        private void SetSelection(bool selected)
        {
            foreach (var account in Accounts)
                account.IsSelected = selected;
        }

        private async Task JoinSelectedAsync()
        {
            var selected = Accounts.Where(x => x.IsSelected).ToList();

            if (!selected.Any())
            {
                Frontend.ShowMessageBox(Strings.Menu_Accounts_JoinNoneSelected, MessageBoxImage.Information);
                return;
            }

            var target = JoinUri.Parse(JoinLink);

            if (target is null && !String.IsNullOrWhiteSpace(JoinLink))
            {
                Frontend.ShowMessageBox(Strings.Menu_Accounts_JoinInvalidLink, MessageBoxImage.Warning);
                return;
            }

            App.Logger.WriteLine("AccountsViewModel::JoinSelected", $"Joining with {selected.Count} account(s)");

            foreach (var account in selected)
            {
                var entry = App.Accounts.Prop.Accounts.FirstOrDefault(x => x.UserId == account.UserId);
                if (entry is null)
                    continue;

                bool started = await AccountsHelper.LaunchAccountAsync(entry, target: target);

                if (!started)
                {
                    App.Logger.WriteLine("AccountsViewModel::JoinSelected", $"Could not launch {account.Username}");
                    continue;
                }

                // give the bootstrapper the chance to grab the launch slot before the next ticket is requested
                await Task.Delay(1500);
            }
        }

        public AccountsViewModel()
        {
            LoadAccounts();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _timer.Tick += (s, e) => UpdateProcessStatus();
            _timer.Start();

            UpdateProcessStatus();
        }

        public void StopTimer() => _timer.Stop();

        private void LoadAccounts()
        {
            Accounts.Clear();
            foreach (var entry in App.Accounts.Prop.Accounts)
            {
                Accounts.Add(CreateUIModel(entry));
            }
        }

        private AccountUIModel CreateUIModel(AccountEntry entry)
        {
            return new AccountUIModel
            {
                Username = entry.Username,
                DisplayName = entry.DisplayName,
                UserId = entry.UserId,
                AvatarUri = entry.AvatarUri,
                DeleteCommand = new RelayCommand<AccountUIModel>(DeleteAccount),
                EditCommand = new RelayCommand<AccountUIModel>(EditAccount)
            };
        }

        private void AddAccount()
        {
            var dialog = new AddAccountDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                var entry = new AccountEntry
                {
                    Username = dialog.Username,
                    DisplayName = dialog.DisplayName,
                    UserId = dialog.UserId,
                    AvatarUri = dialog.AvatarUri,
                    EncryptedCookie = AccountsHelper.Encrypt(dialog.Cookie)
                };

                App.Accounts.Prop.Accounts.Add(entry);
                App.Accounts.Save();

                Accounts.Add(CreateUIModel(entry));
                UpdateProcessStatus();
            }
        }

        private void EditAccount(AccountUIModel? model)
        {
            if (model == null) return;

            var dialog = new AddAccountDialog
            {
                Owner = Application.Current.MainWindow
            };
            // Pre-fill text with hint
            dialog.CookieTextBox.PlaceholderText = Strings.Menu_Accounts_RefreshCookiePlaceholder;

            if (dialog.ShowDialog() == true)
            {
                var entry = App.Accounts.Prop.Accounts.FirstOrDefault(x => x.UserId == model.UserId);
                if (entry != null)
                {
                    entry.EncryptedCookie = AccountsHelper.Encrypt(dialog.Cookie);
                    entry.Username = dialog.Username;
                    entry.DisplayName = dialog.DisplayName;
                    entry.AvatarUri = dialog.AvatarUri;
                    App.Accounts.Save();

                    // Update UI Model
                    model.Username = dialog.Username;
                    model.DisplayName = dialog.DisplayName;
                    model.AvatarUri = dialog.AvatarUri;
                    
                    UpdateProcessStatus();
                }
            }
        }

        private void DeleteAccount(AccountUIModel? model)
        {
            if (model == null) return;

            var result = MessageBox.Show(String.Format(Strings.Menu_Accounts_DeleteConfirm, model.DisplayName), Strings.Menu_Accounts_DeleteConfirm_Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var entry = App.Accounts.Prop.Accounts.FirstOrDefault(x => x.UserId == model.UserId);
                if (entry != null)
                {
                    App.Accounts.Prop.Accounts.Remove(entry);
                    App.Accounts.Save();
                }
                Accounts.Remove(model);
            }
        }

        private static string? FindLogFileForProcess(Process process)
        {
            string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
            if (!Directory.Exists(logsDir)) return null;

            try
            {
                DateTime procStartTime = process.StartTime.ToUniversalTime();
                var logFile = Directory.GetFiles(logsDir, "*.log")
                                       .Concat(Directory.GetFiles(logsDir, "*.txt"))
                                       .Select(f => new FileInfo(f))
                                       .Where(f => f.Name.Contains("Player"))
                                       .OrderBy(f => Math.Abs((f.CreationTimeUtc - procStartTime).TotalSeconds))
                                       .FirstOrDefault();

                if (logFile != null && Math.Abs((logFile.CreationTimeUtc - procStartTime).TotalSeconds) < 120)
                {
                    return logFile.FullName;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountsViewModel", $"Failed to find log file by start time: {ex.Message}");
            }
            return null;
        }

        private static readonly Dictionary<int, long> ProcessUserIdCache = new();

        private static long GetUserIdFromLog(Process process)
        {
            if (ProcessUserIdCache.TryGetValue(process.Id, out long cachedId))
                return cachedId;

            try
            {
                string? logPath = FindLogFileForProcess(process);
                if (!string.IsNullOrEmpty(logPath))
                {
                    var rbxuidRegex = new Regex(@"rbxuid=(?<userId>\d+)");
                    var ticketRegex = new Regex(@"ticket=\{""UserId""%3a(?<userId>\d+)");

                    using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    
                    string? line;
                    long userId = 0;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var rbxuidMatch = rbxuidRegex.Match(line);
                        if (rbxuidMatch.Success) userId = long.Parse(rbxuidMatch.Groups["userId"].Value);

                        var ticketMatch = ticketRegex.Match(line);
                        if (ticketMatch.Success) userId = long.Parse(ticketMatch.Groups["userId"].Value);
                    }

                    if (userId != 0)
                    {
                        ProcessUserIdCache[process.Id] = userId;
                    }
                    return userId;
                }
            }
            catch {}
            return 0;
        }

        private void UpdateProcessStatus()
        {
            try
            {
                var robloxProcesses = Process.GetProcessesByName("RobloxPlayerBeta");
                var runningAccountIds = new Dictionary<long, Process>();

                // Очищаем кэш от закрытых процессов
                var runningPids = robloxProcesses.Select(p => p.Id).ToHashSet();
                var cachedPids = ProcessUserIdCache.Keys.ToList();
                foreach (int pid in cachedPids)
                {
                    if (!runningPids.Contains(pid))
                        ProcessUserIdCache.Remove(pid);
                }

                foreach (var process in robloxProcesses)
                {
                    try
                    {
                        long userId = GetUserIdFromLog(process);
                        if (userId != 0)
                        {
                            runningAccountIds[userId] = process;
                        }
                    }
                    catch {}
                }

                foreach (var account in Accounts)
                {
                    if (runningAccountIds.TryGetValue(account.UserId, out var process))
                    {
                        account.IsRunning = true;
                        try
                        {
                            process.Refresh();
                            double ramMB = process.WorkingSet64 / 1024.0 / 1024.0;
                            account.RamUsage = $"{ramMB:F1} MB";
                        }
                        catch
                        {
                            account.RamUsage = Strings.Common_NotAvailable;
                        }
                    }
                    else
                    {
                        account.IsRunning = false;
                        account.RamUsage = "";
                    }
                }
            }
            catch {}
        }
    }
}
