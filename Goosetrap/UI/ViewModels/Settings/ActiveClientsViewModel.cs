using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.Input;
using Goosetrap.Utility;

namespace Goosetrap.UI.ViewModels.Settings
{
    public class RobloxClientInfo : NotifyPropertyChangedViewModel
    {
        private string _gameName = "Загрузка...";
        private string _username = "Неизвестно";
        private string _displayName = "Неизвестно";

        public int Pid { get; set; }
        public string ExecutablePath { get; set; } = "";
        public string CommandLine { get; set; } = "";
        public string Arguments { get; set; } = "";
        public long PlaceId { get; set; }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string GameName
        {
            get => _gameName;
            set
            {
                _gameName = value;
                OnPropertyChanged(nameof(GameName));
            }
        }

        public ICommand KillCommand { get; set; } = null!;
        public ICommand ReconnectCommand { get; set; } = null!;
    }

    public class RobloxPlaceDetailResponse
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = "";
    }

    public class ActiveClientsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly DispatcherTimer _timer;
        private static readonly Dictionary<long, string> GameNameCache = new();

        public ObservableCollection<RobloxClientInfo> Clients { get; } = new();

        public ICommand RefreshCommand => new RelayCommand(Refresh);

        public ActiveClientsViewModel()
        {
            App.Logger.WriteLine("ActiveClientsViewModel", "Constructor started");
            Refresh();

            // Set up auto-refresh every 3 seconds
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _timer.Tick += (s, e) => Refresh();
            _timer.Start();
            App.Logger.WriteLine("ActiveClientsViewModel", "Constructor finished");
        }

        public void StopTimer()
        {
            _timer.Stop();
        }

        private void Refresh()
        {
            const string LOG_IDENT = "ActiveClientsViewModel::Refresh";

            try
            {
                var runningProcesses = Process.GetProcessesByName("RobloxPlayerBeta");
                var runningPids = runningProcesses.Select(p => p.Id).ToHashSet();

                // 1. Remove clients that are no longer running
                for (int i = Clients.Count - 1; i >= 0; i--)
                {
                    if (!runningPids.Contains(Clients[i].Pid))
                    {
                        Clients.RemoveAt(i);
                    }
                }

                // 2. Add or update running clients
                foreach (var process in runningProcesses)
                {
                    var existingClient = Clients.FirstOrDefault(c => c.Pid == process.Id);
                    if (existingClient != null)
                    {
                        // Если PlaceId ещё 0 (игра загружалась), пробуем перепарсить лог, чтобы получить PlaceId
                        if (existingClient.PlaceId == 0)
                        {
                            var logData = ParseLogFile(process);
                            if (logData.placeId != 0)
                            {
                                existingClient.PlaceId = logData.placeId;
                                _ = FetchGameNameAsync(existingClient);
                            }
                        }
                        continue;
                    }

                    // Fetch Command Line via WMI
                    string cmdLine = GetCommandLine(process.Id) ?? "";

                    // Parse exe path and arguments
                    string exePath = "";
                    string arguments = "";
                    if (!string.IsNullOrEmpty(cmdLine))
                    {
                        if (cmdLine.StartsWith("\""))
                        {
                            int closingQuoteIndex = cmdLine.IndexOf("\"", 1);
                            if (closingQuoteIndex > 0)
                            {
                                exePath = cmdLine.Substring(1, closingQuoteIndex - 1);
                                arguments = cmdLine.Substring(closingQuoteIndex + 1).Trim();
                            }
                        }
                        else
                        {
                            int spaceIndex = cmdLine.IndexOf(" ");
                            if (spaceIndex > 0)
                            {
                                exePath = cmdLine.Substring(0, spaceIndex);
                                arguments = cmdLine.Substring(spaceIndex + 1).Trim();
                            }
                            else
                            {
                                exePath = cmdLine;
                            }
                        }
                    }

                    // Проверяем реестр аккаунтов по тикету из командной строки
                    string username = "Неизвестно";
                    string displayName = "Неизвестно";
                    long placeId = 0;
                    bool foundByTicket = false;
                    
                    // Ищем тикет в аргументах: --gameinfo=TICKET
                    if (!string.IsNullOrEmpty(arguments))
                    {
                        var ticketMatch = System.Text.RegularExpressions.Regex.Match(arguments, @"--gameinfo=(\S+)");
                        if (ticketMatch.Success)
                        {
                            string ticket = ticketMatch.Groups[1].Value;
                            if (AccountPidRegistry.TicketMap.TryGetValue(ticket, out var accountInfo))
                            {
                                username = accountInfo.Username;
                                displayName = accountInfo.DisplayName;
                                foundByTicket = true;
                            }
                        }
                    }

                    // Фолбэк: парсим лог-файл (для запусков через сайт)
                    if (!foundByTicket)
                    {
                        (username, displayName, placeId) = ParseLogFile(process);
                    }
                    else
                    {
                        // PlaceId берём из лога даже если аккаунт определён по тикету
                        var logData = ParseLogFile(process);
                        placeId = logData.placeId;
                    }

                    var clientInfo = new RobloxClientInfo
                    {
                        Pid = process.Id,
                        ExecutablePath = exePath,
                        CommandLine = cmdLine,
                        Arguments = arguments,
                        PlaceId = placeId,
                        Username = username,
                        DisplayName = displayName
                    };

                    clientInfo.KillCommand = new RelayCommand(() => KillClient(clientInfo));
                    clientInfo.ReconnectCommand = new RelayCommand(() => ReconnectClient(clientInfo));

                    Clients.Add(clientInfo);

                    // Fetch game name asynchronously
                    if (placeId != 0)
                    {
                        _ = FetchGameNameAsync(clientInfo);
                    }
                    else
                    {
                        clientInfo.GameName = Strings.Menu_ActiveClients_InMenuOrLoading;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error refreshing Roblox processes: {ex.Message}");
            }
        }

        private void KillClient(RobloxClientInfo client)
        {
            const string LOG_IDENT = "ActiveClientsViewModel::KillClient";
            try
            {
                App.Logger.WriteLine(LOG_IDENT, $"Forcefully terminating Roblox client PID {client.Pid} ({client.Username})");
                
                // Используем taskkill /F для надёжного завершения (обходит Access Denied)
                var killProc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /PID {client.Pid}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                killProc.Start();
                killProc.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to kill Roblox process: {ex.Message}");
            }
            finally
            {
                // Всегда убираем из списка
                Clients.Remove(client);
            }
        }

        private void ReconnectClient(RobloxClientInfo client)
        {
            const string LOG_IDENT = "ActiveClientsViewModel::ReconnectClient";
            try
            {
                App.Logger.WriteLine(LOG_IDENT, $"Reconnecting client PID {client.Pid} ({client.Username})");
                
                // 1. Kill old client
                KillClient(client);

                // Wait a moment for process to fully release
                Task.Delay(500).Wait();

                // 2. Start new client with identical arguments
                var startInfo = new ProcessStartInfo
                {
                    FileName = client.ExecutablePath,
                    Arguments = client.Arguments,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to reconnect client: {ex.Message}");
            }
        }

        private async Task FetchGameNameAsync(RobloxClientInfo client)
        {
            if (GameNameCache.TryGetValue(client.PlaceId, out string? cachedName))
            {
                client.GameName = cachedName;
                return;
            }

            try
            {
                string url = $"https://economy.roblox.com/v2/assets/{client.PlaceId}/details";
                var response = await Http.GetJson<RobloxPlaceDetailResponse>(url);
                if (response != null && !string.IsNullOrEmpty(response.Name))
                {
                    GameNameCache[client.PlaceId] = response.Name;
                    client.GameName = response.Name;
                }
                else
                {
                    client.GameName = string.Format(Strings.Menu_ActiveClients_ProcessLabel, client.PlaceId);
                }
            }
            catch (Exception)
            {
                client.GameName = string.Format(Strings.Menu_ActiveClients_ProcessLabel, client.PlaceId);
            }
        }

        private static string GetCommandLine(int processId)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    return obj["CommandLine"]?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ActiveClientsViewModel", $"Failed to get command line for PID {processId}: {ex.Message}");
            }
            return "";
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
                App.Logger.WriteLine("ActiveClientsViewModel", $"Failed to find log file for process by start time: {ex.Message}");
            }

            return null;
        }

        private static (string username, string displayName, long placeId) ParseLogFile(Process process)
        {
            string username = Strings.Menu_ActiveClients_Unknown;
            string displayName = Strings.Menu_ActiveClients_Unknown;
            long placeId = 0;

            try
            {
                string logPath = FindLogFileForProcess(process);
                if (!string.IsNullOrEmpty(logPath))
                {
                    var rbxuidRegex = new Regex(@"rbxuid=(?<userId>\d+)");
                    var ticketRegex = new Regex(@"ticket=\{""UserId""%3a(?<userId>\d+)%2c""UserName""%3a""(?<username>[^""]+)""%2c""DisplayName""%3a""(?<displayName>[^""]+)""");
                    var placeIdRegex = new Regex(@"placeid:(?<placeId>\d+)");

                    using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    
                    string line;
                    long parsedUserId = 0;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var placeMatch = placeIdRegex.Match(line);
                        if (placeMatch.Success) placeId = long.Parse(placeMatch.Groups["placeId"].Value);

                        var rbxuidMatch = rbxuidRegex.Match(line);
                        if (rbxuidMatch.Success)
                        {
                            parsedUserId = long.Parse(rbxuidMatch.Groups["userId"].Value);
                        }

                        var ticketMatch = ticketRegex.Match(line);
                        if (ticketMatch.Success)
                        {
                            username = Uri.UnescapeDataString(ticketMatch.Groups["username"].Value);
                            displayName = Uri.UnescapeDataString(ticketMatch.Groups["displayName"].Value);
                            parsedUserId = long.Parse(ticketMatch.Groups["userId"].Value);
                        }
                    }

                    if (parsedUserId != 0)
                    {
                        var savedAccount = App.Accounts.Prop.Accounts.FirstOrDefault(x => x.UserId == parsedUserId);
                        if (savedAccount != null)
                        {
                            username = savedAccount.Username;
                            displayName = savedAccount.DisplayName;
                        }
                        else if (username == Strings.Menu_ActiveClients_Unknown)
                        {
                            username = $"User_{parsedUserId}";
                            displayName = username;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ActiveClientsViewModel", $"Failed to parse log file for PID {process.Id}: {ex.Message}");
            }

            return (username, displayName, placeId);
        }
    }
}
