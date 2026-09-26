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
using Goosetrap.RobloxInterfaces;
using Goosetrap.Utility;

namespace Goosetrap.UI.ViewModels.Settings
{
    public class RobloxClientInfo : NotifyPropertyChangedViewModel
    {
        private string _gameName = Strings.Common_Loading;
        private string _username = Strings.Menu_ActiveClients_Unknown;
        private string _displayName = Strings.Menu_ActiveClients_Unknown;
        private string _uptime = "00:00:00";
        private string _ramUsage = "0 MB";

        [JsonIgnore]
        public Process Process { get; set; } = null!;
        public int Pid { get; set; }
        public string ExecutablePath { get; set; } = "";
        public string CommandLine { get; set; } = "";
        public string Arguments { get; set; } = "";
        public long PlaceId { get; set; }
        public long UniverseId { get; set; }
        public string? LogFilePath { get; set; } = "";

        /// <summary>
        /// Roblox account this client belongs to, 0 when it was not started from the account manager.
        /// </summary>
        public long AccountUserId { get; set; }

        /// <summary>
        /// Set when the user closed this client on purpose, so the crash watcher does not restart it.
        /// </summary>
        public bool ClosedByUser { get; set; }

        /// <summary>
        /// Set when Goosetrap closed this client on purpose to bring it back in (memory limit), so the
        /// crash watcher does not schedule a second restart for it.
        /// </summary>
        public bool RestartRequested { get; set; }

        /// <summary>
        /// How much of <see cref="LogFilePath"/> was already parsed, so a refresh only has to look at
        /// the lines that were appended since the last one.
        /// </summary>
        public long LogProcessedBytes { get; set; }

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

        public string Uptime
        {
            get => _uptime;
            set
            {
                _uptime = value;
                OnPropertyChanged(nameof(Uptime));
            }
        }

        public string RamUsage
        {
            get => _ramUsage;
            set
            {
                _ramUsage = value;
                OnPropertyChanged(nameof(RamUsage));
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

        // crash watch: how often an account was restarted inside the current window, the delay,
        // attempt limit and window length themselves come from the settings
        private static readonly Dictionary<long, (int Attempts, DateTime WindowStart)> RestartHistory = new();

        // log scanning: the regexes are compiled once, and only the freshly appended part of a log is
        // read on every refresh - a running client writes several megabytes of log per session and
        // re-reading those in full for every client every tick is what made the page expensive
        private const int LogOverlapBytes = 4096;

        private static readonly Regex PlaceIdLogRegex = new(@"placeid:(?<placeId>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UniverseIdLogRegex = new(@"universeid:(?<universeId>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RbxUidLogRegex = new(@"rbxuid=(?<userId>\d+)", RegexOptions.Compiled);
        private static readonly Regex TicketLogRegex = new(
            @"ticket=\{""UserId""%3a(?<userId>\d+)%2c""UserName""%3a""(?<username>[^""]+)""%2c""DisplayName""%3a""(?<displayName>[^""]+)""",
            RegexOptions.Compiled);

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
                    var goneClient = Clients[i];

                    if (!runningPids.Contains(goneClient.Pid))
                    {
                        Clients.RemoveAt(i);
                        TryRestartCrashedClient(goneClient);
                    }
                }

                // Get all log files currently mapped to existing clients
                var usedLogs = Clients.Where(c => !string.IsNullOrEmpty(c.LogFilePath)).Select(c => c.LogFilePath!).ToHashSet(StringComparer.OrdinalIgnoreCase);
 
                // Update Uptime and RAM for remaining active clients
                foreach (var client in Clients)
                {
                    try
                    {
                        client.Process.Refresh();
                        long ramBytes = client.Process.WorkingSet64;
                        client.RamUsage = $"{ramBytes / 1024 / 1024} MB";

                        TimeSpan uptime = DateTime.Now - client.Process.StartTime;
                        client.Uptime = string.Format("{0:00}:{1:00}:{2:00}", (int)uptime.TotalHours, uptime.Minutes, uptime.Seconds);

                        CheckClientMemory(client, ramBytes);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Error updating client metrics for PID {client.Pid}: {ex.Message}");
                    }
                }

                // 2. Add or update running clients
                foreach (var process in runningProcesses)
                {
                    var existingClient = Clients.FirstOrDefault(c => c.Pid == process.Id);
                    if (existingClient != null)
                    {
                        // only the lines appended since the last tick are parsed, which keeps place
                        // changes (teleports) tracked without re-reading a whole multi-megabyte log
                        UpdateClientFromLogIncremental(existingClient, usedLogs);
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
                    string username = Strings.Menu_ActiveClients_Unknown;
                    string displayName = Strings.Menu_ActiveClients_Unknown;
                    long placeId = 0;
                    long universeId = 0;
                    long accountUserId = 0;
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
                                accountUserId = accountInfo.UserId;
                                foundByTicket = true;
                            }
                        }
                    }

                    // Фолбэк: парсим лог-файл (для запусков через сайт)
                    string? logFilePath = "";
                    long parsedUserId = 0;

                    if (!foundByTicket)
                    {
                        (username, displayName, placeId, universeId, logFilePath, parsedUserId) = ParseLogFile(process, usedLogs);
                    }
                    else
                    {
                        // PlaceId берём из лога даже если аккаунт определён по тикету
                        var logData = ParseLogFile(process, usedLogs);
                        placeId = logData.placeId;
                        universeId = logData.universeId;
                        logFilePath = logData.logFilePath;
                        parsedUserId = logData.userId;
                    }

                    if (!string.IsNullOrEmpty(logFilePath))
                    {
                        usedLogs.Add(logFilePath);
                    }

                    var clientInfo = new RobloxClientInfo
                    {
                        Process = process,
                        Pid = process.Id,
                        ExecutablePath = exePath,
                        CommandLine = cmdLine,
                        Arguments = arguments,
                        PlaceId = placeId,
                        UniverseId = universeId,
                        Username = username,
                        DisplayName = displayName,
                        LogFilePath = logFilePath,
                        AccountUserId = foundByTicket ? accountUserId : parsedUserId
                    };

                    try
                    {
                        long ramBytes = process.WorkingSet64;
                        clientInfo.RamUsage = $"{ramBytes / 1024 / 1024} MB";

                        TimeSpan uptime = DateTime.Now - process.StartTime;
                        clientInfo.Uptime = string.Format("{0:00}:{1:00}:{2:00}", (int)uptime.TotalHours, uptime.Minutes, uptime.Seconds);
                    }
                    catch
                    {
                        // Safe default
                    }

                    clientInfo.KillCommand = new RelayCommand(() => KillClient(clientInfo));
                    clientInfo.ReconnectCommand = new RelayCommand(() => ReconnectClient(clientInfo));

                    // the log was already parsed in full above, so remember how far we got
                    if (!String.IsNullOrEmpty(clientInfo.LogFilePath) && File.Exists(clientInfo.LogFilePath))
                    {
                        try
                        {
                            clientInfo.LogProcessedBytes = new FileInfo(clientInfo.LogFilePath).Length;
                        }
                        catch
                        {
                            // if this fails the next refresh just parses the file once more
                        }
                    }

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

        /// <summary>
        /// Parses only the part of a client's log that was appended since the last refresh and updates
        /// the client with whatever was found there: the place it is in, the account behind it and the
        /// user name once authentication went through.
        /// </summary>
        private void UpdateClientFromLogIncremental(RobloxClientInfo client, HashSet<string> usedLogs)
        {
            const string LOG_IDENT = "ActiveClientsViewModel::UpdateClientFromLog";

            try
            {
                if (String.IsNullOrEmpty(client.LogFilePath))
                {
                    string? found = FindLogFileForProcess(client.Process, usedLogs);

                    if (String.IsNullOrEmpty(found))
                        return;

                    client.LogFilePath = found;
                    client.LogProcessedBytes = 0;
                    usedLogs.Add(found);
                }

                using var fs = new FileStream(client.LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                if (client.LogProcessedBytes > 0 && fs.Length <= client.LogProcessedBytes)
                    return;

                // read a small overlap as well, so a line that was cut in half by the previous read
                // is still matched completely
                long windowStart = Math.Max(0, client.LogProcessedBytes - LogOverlapBytes);
                fs.Seek(windowStart, SeekOrigin.Begin);

                using var reader = new StreamReader(fs);
                string chunk = reader.ReadToEnd();
                client.LogProcessedBytes = fs.Length;

                bool placeChanged = false;

                foreach (string line in chunk.Split('\n'))
                {
                    var placeMatch = PlaceIdLogRegex.Match(line);
                    if (placeMatch.Success && long.TryParse(placeMatch.Groups["placeId"].Value, out long placeId))
                    {
                        if (placeId != 0 && placeId != client.PlaceId)
                        {
                            client.PlaceId = placeId;
                            placeChanged = true;
                        }
                    }

                    var universeMatch = UniverseIdLogRegex.Match(line);
                    if (universeMatch.Success && long.TryParse(universeMatch.Groups["universeId"].Value, out long universeId))
                        client.UniverseId = universeId;

                    if (client.AccountUserId == 0)
                    {
                        var uidMatch = RbxUidLogRegex.Match(line);
                        if (uidMatch.Success && long.TryParse(uidMatch.Groups["userId"].Value, out long userId))
                            client.AccountUserId = userId;
                    }

                    var ticketMatch = TicketLogRegex.Match(line);
                    if (ticketMatch.Success)
                    {
                        client.Username = Uri.UnescapeDataString(ticketMatch.Groups["username"].Value);
                        client.DisplayName = Uri.UnescapeDataString(ticketMatch.Groups["displayName"].Value);

                        if (client.AccountUserId == 0 && long.TryParse(ticketMatch.Groups["userId"].Value, out long ticketUserId))
                            client.AccountUserId = ticketUserId;

                        var savedAccount = App.Accounts.Prop.Accounts.FirstOrDefault(x => x.UserId == client.AccountUserId);
                        if (savedAccount is not null)
                        {
                            client.Username = savedAccount.Username;
                            client.DisplayName = savedAccount.DisplayName;
                        }
                    }
                }

                if (placeChanged)
                    _ = FetchGameNameAsync(client);
            }
            catch (IOException)
            {
                // the client keeps its log open for writing, the next tick will pick it up
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to update PID {client.Pid} from its log: {ex.Message}");
            }
        }

        private void KillClient(RobloxClientInfo client)
        {
            const string LOG_IDENT = "ActiveClientsViewModel::KillClient";
            try
            {
                // mark it as closed on purpose so the crash watcher leaves it alone
                client.ClosedByUser = true;

                App.Logger.WriteLine(LOG_IDENT, $"Forcefully terminating Roblox client PID {client.Pid} ({client.Username})");

                TerminateProcess(client.Pid);
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

        /// <summary>
        /// Brings a client back in when it was closed on purpose (memory limit) or when it disappeared
        /// without the user closing it (crash, disconnect). Limited per account so a client that dies
        /// immediately does not get restarted in an endless loop.
        /// </summary>
        private void TryRestartCrashedClient(RobloxClientInfo client)
        {
            const string LOG_IDENT = "ActiveClientsViewModel::TryRestartCrashedClient";

            if (client.ClosedByUser || client.RestartRequested)
                return;

            if (!App.Settings.Prop.AutoRestartCrashedClients)
                return;

            // clients that died before they ever got into a game are most likely a broken launch,
            // restarting them would just hammer Roblox
            if (App.Settings.Prop.AutoRestartOnlyIfJoined && client.PlaceId == 0)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {client.Pid} was never in a game, not restarting");
                return;
            }

            ScheduleRestart(client, "crashed or disconnected");
        }

        /// <summary>
        /// Restarts a client whose process is eating too much memory. Independent of the crash watcher
        /// switch - a configured memory limit is an explicit request to restart such clients.
        /// </summary>
        private void CheckClientMemory(RobloxClientInfo client, long ramBytes)
        {
            const string LOG_IDENT = "ActiveClientsViewModel::CheckClientMemory";

            int limitMb = App.Settings.Prop.MaxClientRamMb;

            if (limitMb <= 0 || client.RestartRequested)
                return;

            // only clients we can bring back - a browser launched client has no account behind it
            if (client.AccountUserId == 0 || client.PlaceId == 0)
                return;

            if (ramBytes < (long)limitMb * 1024 * 1024)
                return;

            App.Logger.WriteLine(LOG_IDENT, $"PID {client.Pid} ({client.Username}) is using {ramBytes / 1024 / 1024} MB, limit is {limitMb} MB - restarting it");

            // mark first: the process disappears on the next tick and the crash watcher must not
            // schedule a second restart for the same client
            client.RestartRequested = true;

            TerminateProcess(client.Pid);

            ScheduleRestart(client, "memory limit reached");
        }

        /// <summary>
        /// Queues a re-join for the account behind the given client, honouring the configured delay,
        /// attempt limit and time window.
        /// </summary>
        private void ScheduleRestart(RobloxClientInfo client, string reason)
        {
            const string LOG_IDENT = "ActiveClientsViewModel::ScheduleRestart";

            if (client.AccountUserId == 0)
                return; // not started from the account manager, we do not know which account it was

            var window = TimeSpan.FromMinutes(Math.Max(1, App.Settings.Prop.AutoRestartWindowMinutes));
            int maxAttempts = Math.Max(1, App.Settings.Prop.AutoRestartMaxAttempts);

            lock (RestartHistory)
            {
                if (RestartHistory.TryGetValue(client.AccountUserId, out var history))
                {
                    if (DateTime.UtcNow - history.WindowStart > window)
                        history = (0, DateTime.UtcNow);
                    else if (history.Attempts >= maxAttempts)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Gave up restarting account {client.AccountUserId}, {history.Attempts} attempts inside {window.TotalMinutes:0} minutes");
                        return;
                    }
                }
                else
                {
                    history = (0, DateTime.UtcNow);
                }

                RestartHistory[client.AccountUserId] = (history.Attempts + 1, history.WindowStart);
            }

            var entry = App.Accounts.Prop.Accounts.FirstOrDefault(x => x.UserId == client.AccountUserId);

            if (entry is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Account {client.AccountUserId} is no longer saved, not restarting");
                return;
            }

            AccountPidRegistry.LaunchIntent.TryGetValue(client.AccountUserId, out var target);

            // fall back to whatever place the client was in - better than only signing in again
            if (target is null && client.PlaceId != 0)
                target = new JoinTarget(client.PlaceId, null);

            App.Logger.WriteLine(LOG_IDENT, $"Restarting {entry.Username} ({reason}, PID {client.Pid})");

            Task.Run(async () =>
            {
                // let Roblox clean up before the next client takes the launch slot
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, App.Settings.Prop.AutoRestartDelaySeconds)));

                bool started = await Goosetrap.Utility.AccountsHelper.LaunchAccountAsync(entry, target: target);

                if (!started)
                    App.Logger.WriteLine(LOG_IDENT, $"Could not restart {entry.Username}");
            });
        }

        /// <summary>
        /// Forcefully ends a Roblox process. taskkill is used because it also works when the client is
        /// running elevated and OpenProcess would be denied.
        /// </summary>
        private static void TerminateProcess(int pid)
        {
            try
            {
                using var killProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/F /PID {pid}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                killProc?.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ActiveClientsViewModel::TerminateProcess", $"Failed to kill PID {pid}: {ex.Message}");
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
                // First try to get Universe ID, then Universe Name (e.g. "Pet Simulator 99" instead of "Fantasy World")
                try
                {
                    long universeId = client.UniverseId;
                    
                    if (universeId == 0)
                    {
                        string url1 = $"https://games.roblox.com/v1/games/multiget-place-details?placeIds={client.PlaceId}";
                        string json1 = await App.HttpClient.GetStringAsync(url1);
                        using var doc1 = JsonDocument.Parse(json1);
                        var root1 = doc1.RootElement;
                        if (root1.ValueKind == JsonValueKind.Array && root1.GetArrayLength() > 0)
                        {
                            universeId = root1[0].GetProperty("universeId").GetInt64();
                        }
                    }
                    
                    if (universeId != 0)
                    {
                        string url2 = $"https://games.roblox.com/v1/games?universeIds={universeId}";
                        string json2 = await App.HttpClient.GetStringAsync(url2);
                        using var doc2 = JsonDocument.Parse(json2);
                        var root2 = doc2.RootElement;
                        if (root2.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array && dataProp.GetArrayLength() > 0)
                        {
                            string? name = dataProp[0].GetProperty("name").GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                GameNameCache[client.PlaceId] = name;
                                client.GameName = name;
                                return;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore exceptions here so we can fallback to the economy API
                }

                // Fallback to place name if universe fetch fails
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

        private static string? FindLogFileForProcess(Process process, HashSet<string> usedLogs)
        {
            string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
            if (!Directory.Exists(logsDir)) return null;

            try
            {
                DateTime procStartTime = process.StartTime.ToUniversalTime();
                var logFile = Directory.GetFiles(logsDir, "*.log")
                                       .Concat(Directory.GetFiles(logsDir, "*.txt"))
                                       .Select(f => new FileInfo(f))
                                       .Where(f => f.Name.Contains("Player") && !usedLogs.Contains(f.FullName))
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

        private static (string username, string displayName, long placeId, long universeId, string? logFilePath, long userId) ParseLogFile(Process process, HashSet<string> usedLogs, string? preExistingLogPath = null)
        {
            string username = Strings.Menu_ActiveClients_Unknown;
            string displayName = Strings.Menu_ActiveClients_Unknown;
            long placeId = 0;
            long universeId = 0;
            long parsedUserId = 0;
            string? logPath = !string.IsNullOrEmpty(preExistingLogPath) ? preExistingLogPath : FindLogFileForProcess(process, usedLogs);

            try
            {
                if (!string.IsNullOrEmpty(logPath))
                {
                    var rbxuidRegex = new Regex(@"rbxuid=(?<userId>\d+)");
                    var ticketRegex = new Regex(@"ticket=\{""UserId""%3a(?<userId>\d+)%2c""UserName""%3a""(?<username>[^""]+)""%2c""DisplayName""%3a""(?<displayName>[^""]+)""");
                    var placeIdRegex = new Regex(@"placeid:(?<placeId>\d+)");
                    var universeIdRegex = new Regex(@"universeid:(?<universeId>\d+)", RegexOptions.IgnoreCase);

                    using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    
                    string? line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var placeMatch = placeIdRegex.Match(line);
                        if (placeMatch.Success) placeId = long.Parse(placeMatch.Groups["placeId"].Value);

                        var universeMatch = universeIdRegex.Match(line);
                        if (universeMatch.Success) universeId = long.Parse(universeMatch.Groups["universeId"].Value);

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

            return (username, displayName, placeId, universeId, logPath, parsedUserId);
        }
    }
}
