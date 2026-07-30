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
using Goosetrap.UI.Elements.Dialogs;
using Goosetrap.Utility;

namespace Goosetrap.UI.ViewModels.Settings
{
    // Маппинг тикета (gameinfo) → данные аккаунта
    // Тикет сохраняется в аргументах командной строки даже если Roblox перезапустит процесс
    public static class AccountPidRegistry
    {
        public static readonly Dictionary<string, (string Username, string DisplayName, long UserId)> TicketMap = new();
    }

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

        public string StatusText => IsRunning ? "Активен" : "Не запущен";
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

                string cookie = AccountsHelper.Decrypt(entry.EncryptedCookie);
                if (string.IsNullOrEmpty(cookie))
                {
                    Frontend.ShowMessageBox(Strings.Menu_Accounts_DecryptError, MessageBoxImage.Error);
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Requesting launch ticket for {Username}...");
                string ticket = await AccountsHelper.GetLaunchTicketAsync(cookie);
                if (string.IsNullOrEmpty(ticket))
                {
                    Frontend.ShowMessageBox(Strings.Menu_Accounts_TicketError, MessageBoxImage.Error);
                    return;
                }

                // Запускаем Roblox через наш бутстраппер Goosetrap.exe с указанием ID аккаунта для отложенной авторизации
                string launchArgs = $"--app --gameinfo={ticket} --launchtime={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                App.Logger.WriteLine(LOG_IDENT, $"Launching Roblox via Goosetrap bootstrapper for {Username}...");

                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = $"-player \"{launchArgs}\" -account {UserId}",
                    WorkingDirectory = Path.GetDirectoryName(Paths.Process),
                    UseShellExecute = false
                });

                // Регистрируем тикет → аккаунт, чтобы ActiveClients отображал правильное имя
                // Тикет остаётся в командной строке даже если Roblox перезапустит процесс с новым PID
                AccountPidRegistry.TicketMap[ticket] = (Username, DisplayName, UserId);
                App.Logger.WriteLine(LOG_IDENT, $"Registered ticket for account {Username}");
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
            dialog.CookieTextBox.PlaceholderText = "Вставьте новые куки для обновления сессии...";

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

            var result = MessageBox.Show($"Вы уверены, что хотите удалить аккаунт {model.DisplayName}?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);
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

        private static string FindLogFileForProcess(Process process)
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
                string logPath = FindLogFileForProcess(process);
                if (!string.IsNullOrEmpty(logPath))
                {
                    var rbxuidRegex = new Regex(@"rbxuid=(?<userId>\d+)");
                    var ticketRegex = new Regex(@"ticket=\{""UserId""%3a(?<userId>\d+)");

                    using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    
                    string line;
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
                            account.RamUsage = "Н/Д";
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
