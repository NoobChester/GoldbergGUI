using GoldbergGUI.Core.Models;
using GoldbergGUI.Core.Services;
using GoldbergGUI.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MvvmCross.Commands;
using MvvmCross.Plugin.Messenger;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace GoldbergGUI.Core.ViewModels
{
    public class AppSelectedMessage(object sender, SteamApp selectedApp) : MvxMessage(sender)
    {
        public SteamApp SelectedApp { get; } = selectedApp;
    }
    // ReSharper disable once ClassNeverInstantiated.Global
    public class MainViewModel : MvxNavigationViewModel
    {

        private readonly IMvxNavigationService _navigationService;

        private string _dllPath;
        private string _gameName;
        private int _appId;
        //private SteamApp _currentGame;
        private ObservableCollection<Achievement> _achievements;
        private ObservableCollection<DlcApp> _dlcs;
        private string _accountName;
        private long _steamId;
        private bool _offline;
        private bool _disableNetworking;
        private bool _disableOverlay;

        private string _statusText;

        private readonly ISteamService _steam;
        private readonly IGoldbergService _goldberg;
        private readonly ILogger _log;
        private bool _mainWindowEnabled;
        private bool _goldbergApplied;
        private ObservableCollection<string> _steamLanguages;
        private string _selectedLanguage;
        private readonly ILoggerFactory _logProvider;
        private readonly IMvxMessenger _messenger;
        private readonly MvxSubscriptionToken _token;

        public MainViewModel(ISteamService steam, IGoldbergService goldberg, ILoggerFactory logProvider,
            IMvxNavigationService navigationService, IMvxMessenger messenger) : base(logProvider, navigationService)
        {
            _navigationService = navigationService;
            _messenger = messenger;
            _token = _messenger.Subscribe<AppSelectedMessage>(message =>
            {
                GameName = message.SelectedApp.Name;
                AppId = message.SelectedApp.AppId;
            });
            _steam = steam;
            _goldberg = goldberg;
            _log = logProvider.CreateLogger<MainViewModel>();
            _logProvider = logProvider;
        }

        public override void Prepare()
        {
            base.Prepare();
            Task.Run(async () =>
            {
                //var errorDuringInit = false;
                MainWindowEnabled = false;
                StatusText = "Initializing! Please wait...";
                try
                {
                    SteamLanguages = new ObservableCollection<string>(_goldberg.Languages());
                    await ResetForm().ConfigureAwait(false);
                    await _steam.Initialize(_logProvider.CreateLogger<SteamService>()).ConfigureAwait(false);
                    var globalConfiguration =
                        await _goldberg.Initialize(_logProvider.CreateLogger<GoldbergService>()).ConfigureAwait(false);
                    AccountName = globalConfiguration.AccountName;
                    SteamId = globalConfiguration.UserSteamId;
                    SelectedLanguage = globalConfiguration.Language;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    _log.LogError(e.Message);
                    throw;
                }

                MainWindowEnabled = true;
                StatusText = "Ready.";
            });
        }

        public override async Task Initialize()
        {
            await base.Initialize().ConfigureAwait(false);
        }

        // PROPERTIES //

        public string DllPath
        {
            get => _dllPath;
            private set
            {
                SetProperty(ref _dllPath, value);
            }
        }

        public string GameName
        {
            get => _gameName;
            set
            {
                SetProperty(ref _gameName, value);
            }
        }

        public int AppId
        {
            get => _appId;
            set
            {
                if (SetProperty(ref _appId, value))
                {
                    _ = GetNameById();
                }
            }
        }

        // ReSharper disable once InconsistentNaming
        public ObservableCollection<DlcApp> DLCs
        {
            get => _dlcs;
            set
            {
                SetProperty(ref _dlcs, value);
            }
        }

        public ObservableCollection<Achievement> Achievements
        {
            get => _achievements;
            set
            {
                SetProperty(ref _achievements, value);
            }
        }

        public string AccountName
        {
            get => _accountName;
            set
            {
                SetProperty(ref _accountName, value);
            }
        }

        public long SteamId
        {
            get => _steamId;
            set
            {
                SetProperty(ref _steamId, value);
            }
        }

        public bool Offline
        {
            get => _offline;
            set
            {
                SetProperty(ref _offline, value);
            }
        }

        public bool DisableNetworking
        {
            get => _disableNetworking;
            set
            {
                SetProperty(ref _disableNetworking, value);
            }
        }

        public bool DisableOverlay
        {
            get => _disableOverlay;
            set
            {
                SetProperty(ref _disableOverlay, value);
            }
        }

        public bool MainWindowEnabled
        {
            get => _mainWindowEnabled;
            set
            {
                SetProperty(ref _mainWindowEnabled, value);
            }
        }

        public bool GoldbergApplied
        {
            get => _goldbergApplied;
            set
            {
                SetProperty(ref _goldbergApplied, value);
            }
        }

        public bool SteamInterfacesTxtExists
        {
            get
            {
                var dllPathDirExists = GetDllPathDir(out var dirPath);
                return dllPathDirExists && !File.Exists(Path.Combine(dirPath, "steam_interfaces.txt"));
            }
        }

        public bool DllSelected
        {
            get
            {
                var value = !DllPath.Contains("Path to game's steam_api(64).dll");
                if (!value) _log.LogWarning("No DLL selected! Skipping...");
                return value;
            }
        }

        public ObservableCollection<string> SteamLanguages
        {
            get => _steamLanguages;
            set
            {
                SetProperty(ref _steamLanguages, value);
            }
        }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    //MyLogger.Log.LogDebug($"Lang: {value}");
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                SetProperty(ref _statusText, value);
            }
        }

        public static string AboutVersionText =>
            FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;

        public static GlobalHelp G => new();

        // COMMANDS //

        public IMvxCommand OpenFileCommand => new MvxAsyncCommand(OpenFile);

        private async Task OpenFile()
        {
            MainWindowEnabled = false;
            StatusText = "Please choose a file...";
            var dialog = new OpenFileDialog
            {
                Filter = "SteamAPI DLL|steam_api.dll;steam_api64.dll|" +
                         "All files (*.*)|*.*",
                Multiselect = false,
                Title = "Select SteamAPI DLL..."
            };
            if (dialog.ShowDialog() != true)
            {
                MainWindowEnabled = true;
                _log.LogWarning("File selection canceled.");
                StatusText = "No file selected! Ready.";
                return;
            }

            DllPath = dialog.FileName;
            await RaisePropertyChanged(nameof(DllSelected)).ConfigureAwait(false);
            await ReadConfig().ConfigureAwait(false);
            if (!GoldbergApplied) await GetListOfDlc().ConfigureAwait(false);
            MainWindowEnabled = true;
            StatusText = "Ready.";
        }

        public IMvxCommand FindIdCommand => new MvxAsyncCommand(FindId);

        private async Task FindId()
        {
            async Task FindIdInList(SteamApp[] steamApps)
            {
                await _navigationService
                    .Navigate<SearchResultViewModel, IEnumerable<SteamApp>>(steamApps)
                    .ConfigureAwait(false);
            }

            if (GameName.Contains("Game name..."))
            {
                _log.LogError("No game name entered!");
                return;
            }

            MainWindowEnabled = false;
            StatusText = "Trying to find AppID...";
            var appByName = await _steam.GetAppByName(_gameName).ConfigureAwait(false);
            if (appByName != null)
            {
                GameName = appByName.Name;
                AppId = appByName.AppId;
            }
            else
            {
                var list = await _steam.GetListOfAppsByName(GameName).ConfigureAwait(false);
                var steamApps = list as SteamApp[] ?? list.ToArray();
                if (steamApps.Length == 1)
                {
                    var steamApp = steamApps[0];
                    if (steamApp != null)
                    {
                        GameName = steamApp.Name;
                        AppId = steamApp.AppId;
                    }
                    else
                    {
                        await FindIdInList(steamApps).ConfigureAwait(false);
                    }
                }
                else
                {
                    await FindIdInList(steamApps).ConfigureAwait(false);
                }
            }
            await GetListOfDlc().ConfigureAwait(false);
            MainWindowEnabled = true;
            StatusText = "Ready.";
        }

        //public IMvxCommand GetNameByIdCommand => new MvxAsyncCommand(GetNameById);

        private async Task GetNameById()
        {
            if (AppId <= 0)
            {
                _log.LogError("Invalid Steam App!");
                return;
            }

            if (!string.IsNullOrEmpty(GameName)) return;

            var steamApp = await _steam.GetAppById(AppId).ConfigureAwait(false);
            if (steamApp != null) GameName = steamApp.Name;
        }

        public IMvxCommand GetListOfAchievementsCommand => new MvxAsyncCommand(GetListOfAchievements);

        private async Task GetListOfAchievements()
        {
            if (AppId <= 0)
            {
                _log.LogError("Invalid Steam App!");
                return;
            }

            MainWindowEnabled = false;
            StatusText = "Trying to get list of achievements...";
            var listOfAchievements = await _steam.GetListOfAchievements(new SteamApp { AppId = AppId, Name = GameName });
            Achievements = new MvxObservableCollection<Achievement>(listOfAchievements);
            MainWindowEnabled = true;

            if (Achievements.Count > 0)
            {
                var empty = Achievements.Count == 1 ? "" : "s";
                StatusText = $"Successfully got {Achievements.Count} achievement{empty}! Ready.";
            }
            else
            {
                StatusText = "No achievements found! Ready.";
            }
        }

        public IMvxCommand GetListOfDlcCommand => new MvxAsyncCommand(GetListOfDlc);

        private async Task GetListOfDlc()
        {
            if (AppId <= 0)
            {
                _log.LogError("Invalid Steam App!");
                return;
            }

            MainWindowEnabled = false;
            StatusText = "Trying to get list of DLCs...";
            var listOfDlc = await _steam.GetListOfDlc(new SteamApp { AppId = AppId, Name = GameName }, true)
                .ConfigureAwait(false);
            DLCs = new MvxObservableCollection<DlcApp>(listOfDlc);
            MainWindowEnabled = true;
            if (DLCs.Count > 0)
            {
                var empty = DLCs.Count == 1 ? "" : "s";
                StatusText = $"Successfully got {DLCs.Count} DLC{empty}! Ready.";
            }
            else
            {
                StatusText = "No DLC found! Ready.";
            }
        }

        public IMvxCommand SaveConfigCommand => new MvxAsyncCommand(SaveConfig);

        private async Task SaveConfig()
        {
            _log.LogInformation("Saving global settings...");
            var globalConfiguration = new GoldbergGlobalConfiguration
            {
                AccountName = AccountName,
                UserSteamId = SteamId,
                Language = SelectedLanguage
            };
            await _goldberg.SetGlobalSettings(globalConfiguration).ConfigureAwait(false);
            if (!DllSelected) return;

            _log.LogInformation("Saving Goldberg settings...");
            if (!GetDllPathDir(out var dirPath)) return;
            MainWindowEnabled = false;
            StatusText = "Saving...";
            await _goldberg.Save(dirPath, new GoldbergConfiguration
            {
                AppId = AppId,
                Achievements = Achievements.ToList(),
                DlcList = DLCs.ToList(),
                Offline = Offline,
                DisableNetworking = DisableNetworking,
                DisableOverlay = DisableOverlay
            }
            ).ConfigureAwait(false);
            GoldbergApplied = _goldberg.GoldbergApplied(dirPath);
            MainWindowEnabled = true;
            StatusText = "Ready.";
        }

        public IMvxCommand ResetConfigCommand => new MvxAsyncCommand(ResetConfig);

        private async Task ResetConfig()
        {
            var globalConfiguration = await _goldberg.GetGlobalSettings().ConfigureAwait(false);
            AccountName = globalConfiguration.AccountName;
            SteamId = globalConfiguration.UserSteamId;
            SelectedLanguage = globalConfiguration.Language;
            if (!DllSelected) return;

            _log.LogInformation("Reset form...");
            MainWindowEnabled = false;
            StatusText = "Resetting...";
            await ReadConfig().ConfigureAwait(false);
            MainWindowEnabled = true;
            StatusText = "Ready.";
        }

        public IMvxCommand GenerateSteamInterfacesCommand => new MvxAsyncCommand(GenerateSteamInterfaces);

        private async Task GenerateSteamInterfaces()
        {
            if (!DllSelected) return;

            _log.LogInformation("Generate steam_interfaces.txt...");
            MainWindowEnabled = false;
            StatusText = @"Generating ""steam_interfaces.txt"".";
            GetDllPathDir(out var dirPath);
            if (File.Exists(Path.Combine(dirPath, "steam_api_o.dll")))
                await _goldberg.GenerateInterfacesFile(Path.Combine(dirPath, "steam_api_o.dll")).ConfigureAwait(false);
            else if (File.Exists(Path.Combine(dirPath, "steam_api64_o.dll")))
                await _goldberg.GenerateInterfacesFile(Path.Combine(dirPath, "steam_api64_o.dll"))
                    .ConfigureAwait(false);
            else await _goldberg.GenerateInterfacesFile(DllPath).ConfigureAwait(false);
            await RaisePropertyChanged(nameof(SteamInterfacesTxtExists));
            MainWindowEnabled = true;
            StatusText = "Ready.";
        }

        public IMvxCommand PasteDlcCommand => new MvxCommand(() =>
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            _log.LogInformation("Trying to paste DLC list...");
            if (!(Clipboard.ContainsText(TextDataFormat.UnicodeText) || Clipboard.ContainsText(TextDataFormat.Text)))
            {
                _log.LogWarning("Invalid DLC list!");
            }
            else
            {
                var result = Clipboard.GetText();
                var expression = new Regex(@"(?<id>.*) *= *(?<name>.*)");
                var pastedDlc = (from line in result.Split(new[] { "\n", "\r\n" },
                    StringSplitOptions.RemoveEmptyEntries)
                                 select expression.Match(line) into match
                                 where match.Success
                                 select new DlcApp
                                 {
                                     AppId = Convert.ToInt32(match.Groups["id"].Value),
                                     Name = match.Groups["name"].Value
                                 }).ToList();
                if (pastedDlc.Count > 0)
                {
                    DLCs.Clear();
                    DLCs = new ObservableCollection<DlcApp>(pastedDlc);
                    //var empty = DLCs.Count == 1 ? "" : "s";
                    //StatusText = $"Successfully got {DLCs.Count} DLC{empty} from clipboard! Ready.";
                    var statusTextCount = DLCs.Count == 1 ? "one DLC" : $"{DLCs.Count} DLCs";
                    StatusText = $"Successfully got {statusTextCount} from clipboard! Ready.";
                }
                else
                {
                    StatusText = "No DLC found in clipboard! Ready.";
                }
            }
        });

        public IMvxCommand OpenGlobalSettingsFolderCommand => new MvxCommand(OpenGlobalSettingsFolder);

        private void OpenGlobalSettingsFolder()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                StatusText = "Can't open folder (Windows only)! Ready.";
                return;
            }

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Goldberg SteamEmu Saves", "settings");
            var start = Process.Start("explorer.exe", path);
            start?.Dispose();
        }

        // OTHER METHODS //

        private async Task ResetForm()
        {
            DllPath = "Path to game's steam_api(64).dll...";
            await RaisePropertyChanged(nameof(DllSelected)).ConfigureAwait(false);
            await RaisePropertyChanged(nameof(SteamInterfacesTxtExists)).ConfigureAwait(false);
            GameName = "Game name...";
            AppId = -1;
            Achievements = new MvxObservableCollection<Achievement>();
            DLCs = new MvxObservableCollection<DlcApp>();
            AccountName = "Account name...";
            SteamId = -1;
            Offline = false;
            DisableNetworking = false;
            DisableOverlay = false;
        }

        private async Task ReadConfig()
        {
            if (!GetDllPathDir(out var dirPath)) return;
            var config = await _goldberg.Read(dirPath).ConfigureAwait(false);
            SetFormFromConfig(config);
            GoldbergApplied = _goldberg.GoldbergApplied(dirPath);
            await RaisePropertyChanged(nameof(SteamInterfacesTxtExists));
        }

        private void SetFormFromConfig(GoldbergConfiguration config)
        {
            AppId = config.AppId;
            Achievements = new ObservableCollection<Achievement>(config.Achievements);
            DLCs = new ObservableCollection<DlcApp>(config.DlcList);
            Offline = config.Offline;
            DisableNetworking = config.DisableNetworking;
            DisableOverlay = config.DisableOverlay;
        }

        private bool GetDllPathDir(out string dirPath)
        {
            if (!DllSelected)
            {
                dirPath = null;
                return false;
            }

            dirPath = Path.GetDirectoryName(DllPath);
            if (dirPath != null) return true;

            _log.LogError($"Invalid directory for {DllPath}.");
            return false;
        }
    }
}