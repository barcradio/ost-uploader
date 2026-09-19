using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Path = System.IO.Path;
using ost_uploader.ViewModels;

namespace ost_uploader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string AppName = "OST Uploader";
        const string AppAuthor = "Bridgerland Amateur Radio Club";
        const string AppCopyright = "2025 Bridgerland Amateur Radio Club";
        const string AppLicense = "MIT License";
        const string AppDescription = "A simple tool to upload UltraTracker data to OpenSplitTime.org";
        const string AppRepository = "https://github.com/barcradio/ost-uploader";
        const string AppIcon = "pack://application:,,,/ost-uploader;component/Resources/ost_icon.ico";

        private string _apiBaseUrl = "https://www.opensplittime.org";
        const int TEST_EVENT_ID = 1015;
        const int TEST_EVENT_GROUP = 833;
        const int BEAR1002025_EVENT_ID = 995;
        const int BEAR1002025_EVENT_GROUP = 822;
        private int _targetEventId = BEAR1002025_EVENT_ID;
        private string _eventName = string.Empty;
        private string _statusMessage = string.Empty;

        private APIAuthResponse? _authResponse;
        public event EventHandler? TimesLoaded;

        StatusBarViewModel _statusBarViewModel;

        private bool _isAuthenticated = false;
        private SecureCredentialStore _credentialStore;
        private bool _suppressSitePrompt = false;
        private int _lastSiteIndex = -1;
        private List<SiteEntry> _siteEntries = new();
        private string _loadedFilePath = string.Empty;
        private bool _hasLoadedTimes = false;
        private string? _json = null;
        private readonly EventFilePreferenceStore _eventFilePreferenceStore = new EventFilePreferenceStore();
        private StationSplitMapper _stationNameMap = new StationSplitMapper();
        private string? _eventFilePath = null;
        private Dictionary<string, HashSet<string>> _splitEntryKindsBySplit =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private string? _splitKindSyncStatusMessage;

        public MainWindow()
        {
            _suppressSitePrompt = true;
            _credentialStore = new SecureCredentialStore();
            InitializeComponent();
            // Initialize API base from site selector (default to Production)
            _apiBaseUrl = GetBaseUrlFromSelection();
            // record initial selection index
            _lastSiteIndex = siteComboBox?.SelectedIndex ?? -1;
            _statusBarViewModel = new StatusBarViewModel();
            this.DataContext = _statusBarViewModel;

            event_textBox.IsReadOnly = true;
            station_textBox.IsReadOnly = true;
            exportType_textBox.IsReadOnly = true;
            csvDataGrid.IsReadOnly = true;

            recordsLoaded_Label.Content = UiStrings.Format(UiStrings.Import_RecordsLoaded, 0);
            _statusBarViewModel.StatusMessage = UiStrings.Event_LoadEventZipStatus;
            _statusBarViewModel.OSTEventName = $"{_targetEventId}";

            // Authentication is unavailable until valid OST metadata is loaded from stations.json in an event zip.
            loginButton.IsEnabled = false;
            userEmail_TextBox.IsEnabled = false;
            password_TextBox.IsEnabled = false;
            saveToken_CheckBox.IsEnabled = false;

            _suppressSitePrompt = false;

            if (_eventFilePreferenceStore.TryGetExistingPath(out var lastEventFilePath))
                LoadEventFileFromPath(lastEventFilePath, showErrors: false);

            this.Show();
        }

        private async void authLogin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button loginButton)
            {
                string userEmail = this.userEmail_TextBox.Text;
                SecureString password = password_TextBox.SecurePassword;
                await AuthenticateAsync(userEmail, password);

                if (_isAuthenticated && _authResponse != null)
                {
                    var expiration = FormatTokenExpiration(_authResponse.expiration);
                    authStatus_Label.Content = UiStrings.Format(UiStrings.Auth_Authenticated, expiration);
                    authStatus_Label.ToolTip = UiStrings.Format(UiStrings.Auth_TokenExpiration, expiration);
                    authStatus_Label.Visibility = Visibility.Visible;
                    authStatus_Label.Foreground = Brushes.Black;
                    loginButton.IsEnabled = false;
                    // Save token if user requested it
                    try
                    {
                        if (saveToken_CheckBox != null && saveToken_CheckBox.IsChecked == true)
                        {
                            _credentialStore.SaveToken(_authResponse, _apiBaseUrl, userEmail, password);
                        }
                        else
                        {
                            // If checkbox not checked, ensure any previously saved token is removed
                            _credentialStore.Clear();
                        }
                    }
                    catch
                    {
                        // ignore save errors
                    }
                }
                else
                {
                    authStatus_Label.Content = UiStrings.Auth_AuthenticationFailed;
                    authStatus_Label.ToolTip = UiStrings.Auth_LoginFailedTooltip;
                    authStatus_Label.Visibility = Visibility.Visible;
                    authStatus_Label.Foreground = Brushes.Red;
                    loginButton.IsEnabled = true;
                }
            }
        }

        private async Task AuthenticateAsync(string userEmail, SecureString password)
        {
            if (string.IsNullOrWhiteSpace(userEmail) || password == null || password.Length == 0)
                return;

            _isAuthenticated = false;

            var authBaseUrl = GetAuthBaseUrl(_apiBaseUrl);
            var authProvider = new AuthProvider(authBaseUrl);
            await authProvider.SignInBasicAsync(userEmail, password);

            _authResponse = authProvider.AuthResponse;

            _isAuthenticated = !string.IsNullOrWhiteSpace(_authResponse.token);
            if (_isAuthenticated)
            {
                await SyncSplitEntryKindsAsync();
                _statusBarViewModel.StatusMessage = _splitKindSyncStatusMessage ?? UiStrings.Status_Ready;
            }
        }

        private async Task SyncSplitEntryKindsAsync()
        {
            _splitEntryKindsBySplit = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _splitKindSyncStatusMessage = null;

            if (_authResponse == null || string.IsNullOrWhiteSpace(_authResponse.token))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                _splitKindSyncStatusMessage = UiStrings.Status_SplitKindSyncUnavailableNoSite;
                return;
            }

            try
            {
                var apiClient = new OpenSplitTimeApiClient(_apiBaseUrl, _authResponse.token);
                var response = await apiClient.GetAsync(string.Empty);
                var eventGroup = JsonSerializer.Deserialize<OSTEventGroup>(response);
                _splitEntryKindsBySplit = BuildSplitEntryKindsLookup(eventGroup);
            }
            catch (Exception ex)
            {
                _splitKindSyncStatusMessage =
                    UiStrings.Format(UiStrings.Status_SplitKindSyncUnavailableError, ex.Message);
            }
        }

        private static Dictionary<string, HashSet<string>> BuildSplitEntryKindsLookup(OSTEventGroup? eventGroup)
        {
            var splitEntryKinds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var attributes = eventGroup?.data?.attributes;
            var groups = new List<DataEntryGroup>();

            if (attributes?.dataEntryGroups != null)
            {
                groups.AddRange(attributes.dataEntryGroups);
            }

            if (attributes?.unpairedDataEntryGroups != null)
            {
                groups.AddRange(attributes.unpairedDataEntryGroups);
            }

            foreach (var group in groups)
            {
                if (group?.entries == null)
                {
                    continue;
                }

                foreach (var entry in group.entries)
                {
                    var splitName = entry?.splitName?.Trim();
                    if (entry == null || string.IsNullOrWhiteSpace(splitName) || string.IsNullOrWhiteSpace(entry.subSplitKind))
                    {
                        continue;
                    }

                    if (!splitEntryKinds.TryGetValue(splitName, out var allowedKinds))
                    {
                        allowedKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        splitEntryKinds[splitName] = allowedKinds;
                    }

                    foreach (var kind in NormalizeSplitKinds(entry.subSplitKind))
                    {
                        allowedKinds.Add(kind);
                    }
                }
            }

            return splitEntryKinds;
        }

        private static IEnumerable<string> NormalizeSplitKinds(string rawKind)
        {
            var normalized = rawKind.Trim().ToLowerInvariant();
            return normalized switch
            {
                "in" => new[] { "in" },
                "out" => new[] { "out" },
                "inout" => new[] { "in", "out" },
                _ => Array.Empty<string>()
            };
        }

        private IEnumerable<string> ResolveAllowedKindsForSplit(string splitName)
        {
            if (_splitEntryKindsBySplit.TryGetValue(splitName, out var allowedKinds))
            {
                return allowedKinds;
            }

            return new[] { "in", "out" };
        }

        private string GetBaseUrlFromSelection()
        {
            try
            {
                if (siteComboBox == null) return "";

                if (siteComboBox.SelectedItem is SiteEntry site)
                    return site.Url;

                var index = siteComboBox.SelectedIndex;
                if (index >= 0 && index < _siteEntries.Count)
                    return _siteEntries[index].Url;

                if (siteComboBox.SelectedItem is string selectedTitle)
                {
                    var match = _siteEntries.FirstOrDefault(s => string.Equals(s.Title, selectedTitle, StringComparison.OrdinalIgnoreCase));
                    return match?.Url ?? string.Empty;
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetAuthBaseUrl(string connectionUrl)
        {
            if (string.IsNullOrWhiteSpace(connectionUrl))
                return "https://www.opensplittime.org";

            if (!Uri.TryCreate(connectionUrl, UriKind.Absolute, out var uri))
                return "https://www.opensplittime.org";

            var authority = uri.IsDefaultPort ? uri.Authority.Replace(":80", string.Empty).Replace(":443", string.Empty) : uri.Authority;
            return $"{uri.Scheme}://{authority}";
        }

        private static string FormatTokenExpiration(string expiration)
        {
            if (DateTimeOffset.TryParse(
                expiration,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return parsed.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            }

            return expiration;
        }

        private void siteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (password_TextBox == null)
                return;

            // If we're programmatically changing the selection (e.g., because an event file loaded), don't prompt.
            if (_suppressSitePrompt)
            {
                _apiBaseUrl = GetBaseUrlFromSelection();
                _lastSiteIndex = siteComboBox?.SelectedIndex ?? _lastSiteIndex;
                return;
            }

            // User-driven change: determine if index changed
            var newIndex = siteComboBox?.SelectedIndex ?? -1;
            if (newIndex != _lastSiteIndex)
            {
                var newUrl = GetBaseUrlFromSelection();
                // If switching to a production-like URL, require confirmation
                bool isProduction = IsProductionUrl(newUrl);
                if (isProduction)
                {
                    var result = MessageBox.Show(UiStrings.Event_ProductionSwitchMessage, UiStrings.Event_ConfirmProduction, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes)
                    {
                        // revert selection
                        _suppressSitePrompt = true;
                        try
                        {
                            if (siteComboBox != null)
                                siteComboBox.SelectedIndex = _lastSiteIndex;
                        }
                        finally { _suppressSitePrompt = false; }
                        return;
                    }
                }

                // Accept the change: update base URL and de-authenticate
                _apiBaseUrl = newUrl;
                _lastSiteIndex = newIndex;
                _splitEntryKindsBySplit = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                _splitKindSyncStatusMessage = null;
                password_TextBox.Clear();
                saveToken_CheckBox.IsChecked = false;
                RestoreSavedCredentialsForSite(newUrl);

                _statusBarViewModel.OSTEventName = GetSelectedSiteEventName();
                _statusBarViewModel.StatusMessage = _isAuthenticated ? UiStrings.Status_Ready : UiStrings.Status_WaitingForAuthentication;
            }
        }

        private string GetSelectedSiteEventName()
        {
            var index = siteComboBox?.SelectedIndex ?? -1;
            if (index >= 0 && index < _siteEntries.Count)
                return $"Event: {_siteEntries[index].EventSlug}";

            return _statusBarViewModel.OSTEventName;
        }

        private void RestoreSavedCredentialsForSite(string baseUrl)
        {
            _isAuthenticated = false;
            _authResponse = null;
            authStatus_Label.Content = UiStrings.Auth_NotAuthenticated;
            authStatus_Label.ToolTip = string.Empty;
            authStatus_Label.Visibility = Visibility.Visible;
            authStatus_Label.Foreground = Brushes.Red;
            loginButton.IsEnabled = true;

            if (!_credentialStore.TryGetSavedCredentials(baseUrl, out var email, out var password, out var saved))
                return;

            userEmail_TextBox.Text = email;
            password_TextBox.Password = new System.Net.NetworkCredential(string.Empty, password).Password;
            saveToken_CheckBox.IsChecked = true;

            if (string.IsNullOrWhiteSpace(saved.token) || string.IsNullOrWhiteSpace(saved.expiration) ||
                !DateTime.TryParse(saved.expiration, out var expiration) || expiration.ToUniversalTime() <= DateTime.UtcNow)
                return;

            _authResponse = saved;
            _isAuthenticated = true;
            var formattedExpiration = FormatTokenExpiration(saved.expiration);
            authStatus_Label.Content = UiStrings.Format(UiStrings.Auth_AuthenticatedSaved, formattedExpiration);
            authStatus_Label.ToolTip = UiStrings.Format(UiStrings.Auth_TokenExpiration, formattedExpiration);
            authStatus_Label.Foreground = Brushes.Black;
            loginButton.IsEnabled = false;
        }

        private bool IsProductionUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            var u = url.ToLowerInvariant();
            if (u.Contains("staging") || u.Contains("dev") || u.Contains("test")) return false;
            return true;
        }

        private async Task GetEventNameAsync(int eventId)
        {
            if (_authResponse == null || string.IsNullOrWhiteSpace(_authResponse.token))
            {
                MessageBox.Show(UiStrings.Auth_AuthenticationTokenMissing);
                return;
            }
            try
            {
                var apiClient = new OpenSplitTimeApiClient(_apiBaseUrl, _authResponse.token);
                var response = await apiClient.GetAsync($"/api/v1/events/{eventId}");
                var targetEvent = JsonSerializer.Deserialize<OSTEvent>(response);
                if (targetEvent?.data?.attributes?.name is string eventName)
                    _statusBarViewModel.OSTEventName = eventName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(UiStrings.Format(UiStrings.Event_ErrorRetrievingEvent, ex.Message));
            }
        }

        // Load times from CSV and format JSON for OST. Returns false on failure.
        private bool LoadTimes(string filePath, out string json)
        {
            json = string.Empty;
            var importer = new TimesImporter();
            var (header, entries) = importer.Import(filePath);
            var station = string.Empty;
            var eventname = string.Empty;
            var exportType = string.Empty;

            // If no explicit event file is loaded yet, try auto-detecting an event file next to the CSV
            if (string.IsNullOrWhiteSpace(_eventFilePath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(filePath) ?? ".";
                    var baseNoExt = Path.GetFileNameWithoutExtension(filePath);
                    var candidates = new[] {
                        Path.Combine(dir, baseNoExt + ".zip"),
                        Path.Combine(dir, "event.zip")
                    };

                    string? found = candidates.FirstOrDefault(p => File.Exists(p));
                    if (found != null)
                    {
                        LoadEventFileFromPath(found);
                        _statusBarViewModel.StatusMessage = UiStrings.Event_EventFileLoaded;
                    }
                }
                catch
                {
                    // ignore event file errors and continue with defaults
                }
            }

            if (header == null)
            {
                MessageBox.Show(UiStrings.Import_FailedReadCsvHeader);
                return false;
            }

            if (header.Fields.Count != 3)
            {
                MessageBox.Show(UiStrings.Import_InvalidCsvHeader);
                return false;
            }
            else
            {
                eventname = header.Fields[0];
                station = header.Fields[1];
                exportType = header.Fields[2];

                if (exportType != "full-export" && exportType != "incremental-export")
                {
                    MessageBox.Show(UiStrings.Import_InvalidCsvFile);
                    return false;
                }
            }

            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // Map station token to OST split name.
            string? mappedSplit;
            if (!_stationNameMap.StationSplitMap.TryGetValue(station, out mappedSplit))
            {
                var key = _stationNameMap.StationSplitMap.Keys.FirstOrDefault(k => string.Equals(k, station, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    mappedSplit = _stationNameMap.StationSplitMap[key];
                }
                else
                {
                    // With event-file-driven uploads, missing station mapping is invalid and will be rejected by OST.
                    MessageBox.Show(UiStrings.Format(UiStrings.Import_StationNotFound, station));
                    return false;
                }
            }

            // API schema validation: bib numbers may include only digits or "*" (rows excluded from upload are exempt).
            var invalidBib = entries.FirstOrDefault(e => e.WillUpload && (string.IsNullOrWhiteSpace(e.BibId) || e.BibId.Any(c => !(char.IsDigit(c) || c == '*'))));
            if (invalidBib != null)
            {
                MessageBox.Show(UiStrings.Format(UiStrings.Import_InvalidBib, invalidBib.BibId));
                return false;
            }

            var allowedKinds = ResolveAllowedKindsForSplit(mappedSplit);
            var formatter = new TimesJsonFormatter(AppName + "_" + fileName, mappedSplit!, allowedKinds);
            json = formatter.Format(entries);

            OnTimesLoaded(EventArgs.Empty, entries, header);

            return true;
        }

        private void csvDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == nameof(TimeEntry.IsDuplicate) ||
                e.PropertyName == nameof(TimeEntry.IsDidNotStart) ||
                e.PropertyName == nameof(TimeEntry.IsDidNotStartAtOtherStation) ||
                e.PropertyName == nameof(TimeEntry.NeedsAttention) ||
                e.PropertyName == nameof(TimeEntry.WillUpload))
            {
                e.Cancel = true;
            }
            else if (e.PropertyName == nameof(TimeEntry.ReadyStatus))
            {
                e.Column.Header = UiStrings.Upload_ReadyColumn;
                e.Column.Width = new DataGridLength(52);
            }
            else if (e.PropertyName == nameof(TimeEntry.VerificationNote))
            {
                e.Column.Header = UiStrings.Verification_Column;
                e.Column.MinWidth = 180;
                e.Column.Width = new DataGridLength(4, DataGridLengthUnitType.Star);
            }
            else
            {
                e.Column.Width = e.PropertyName switch
                {
                    nameof(TimeEntry.Index) => new DataGridLength(48),
                    nameof(TimeEntry.Sent) => new DataGridLength(48),
                    nameof(TimeEntry.BibId) => new DataGridLength(55),
                    nameof(TimeEntry.TimeIn) => new DataGridLength(130),
                    nameof(TimeEntry.TimeOut) => new DataGridLength(130),
                    nameof(TimeEntry.DropType) => new DataGridLength(95),
                    nameof(TimeEntry.DropStation) => new DataGridLength(1.25, DataGridLengthUnitType.Star),
                    nameof(TimeEntry.Note) => new DataGridLength(1.75, DataGridLengthUnitType.Star),
                    _ => e.Column.Width
                };

                if (e.PropertyName == nameof(TimeEntry.DropStation))
                {
                    e.Column.MinWidth = 100;
                }
                else if (e.PropertyName == nameof(TimeEntry.Note))
                {
                    e.Column.MinWidth = 120;
                }
            }
        }

        protected virtual void OnTimesLoaded(EventArgs e, List<TimeEntry> entries, CsvHeader header)
        {
            TimesLoaded?.Invoke(this, e);

            event_textBox.Text = header.Fields[0];
            station_textBox.Text = header.Fields[1];
            exportType_textBox.Text = header.Fields[2];
            csvDataGrid.ItemsSource = entries;

            var duplicateCount = entries.Count(e => e.IsDuplicate);
            recordsLoaded_Label.Content = duplicateCount > 0
                ? UiStrings.Format(UiStrings.Import_RecordsLoadedWithDuplicates, entries.Count, duplicateCount)
                : UiStrings.Format(UiStrings.Import_RecordsLoaded, entries.Count);
        }

        private void browse_Button_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Select a CSV file",
                Multiselect = false,
                InitialDirectory = GetUltraTrackerDirectory()
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                _loadedFilePath = openFileDialog.FileName;

                if (!LoadTimes(_loadedFilePath, out _json))
                    return;

                _hasLoadedTimes = true;

                fileName_textBox.Text = _loadedFilePath;
                _statusBarViewModel.StatusMessage = UiStrings.Format(UiStrings.Import_ReadyForUpload, Path.GetFileName(_loadedFilePath));
            }
        }

        private void browseEvent_Button_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Event files (*.zip)|*.zip", 
                Title = "Select an event file",
                Multiselect = false,
                InitialDirectory = GetUltraTrackerDirectory()
            };

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                LoadEventFileFromPath(openFileDialog.FileName);
            }
        }

        private static string GetUltraTrackerDirectory()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var ultraTrackerDirectory = Path.Combine(documents, "Ultra-Tracker");
            return Directory.Exists(ultraTrackerDirectory) ? ultraTrackerDirectory : documents;
        }

        private void LoadEventFileFromPath(string path, bool showErrors = true)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (showErrors)
                    MessageBox.Show(UiStrings.Event_EventFileNotFound);
                return;
            }

            if (EventFileLoader.TryLoad(path, out var data))
            {
                _eventFilePath = path;
                _eventFilePreferenceStore.SavePath(path);
                _stationNameMap = new StationSplitMapper(data.StationMap);
                _splitEntryKindsBySplit = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                _splitKindSyncStatusMessage = null;
                eventFile_textBox.Text = path;
                // Populate siteComboBox from event file Sites (authoritative source of OST endpoints)
                try
                {
                    _suppressSitePrompt = true;
                    siteComboBox.ItemsSource = null;
                    siteComboBox.Items.Clear();

                    // Only enumerate connection options from openSplitTime.production and openSplitTime.staging metadata.
                    var sites = (data.Sites ?? new System.Collections.Generic.List<SiteEntry>())
                        .Where(s => string.Equals(s.Title, "Production", StringComparison.OrdinalIgnoreCase) || string.Equals(s.Title, "Staging", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    _siteEntries = sites;

                    if (sites.Count > 0)
                    {
                        siteComboBox.ItemsSource = sites.Select(s => s.Title).ToList();
                        siteComboBox.IsEnabled = true;

                        // Default selection: prefer staging if present
                        int preferred = -1;
                        for (int i = 0; i < sites.Count; i++)
                        {
                            var tag = sites[i].Url;
                            if (!string.IsNullOrWhiteSpace(tag) && tag.Contains("staging", StringComparison.OrdinalIgnoreCase))
                            {
                                preferred = i; break;
                            }
                        }
                        if (preferred == -1) preferred = 0;
                        siteComboBox.SelectedIndex = preferred;
                        _lastSiteIndex = siteComboBox?.SelectedIndex ?? _lastSiteIndex;
                        _apiBaseUrl = GetBaseUrlFromSelection();

                        // enable authentication controls
                        loginButton.IsEnabled = true;
                        userEmail_TextBox.IsEnabled = true;
                        password_TextBox.IsEnabled = true;
                        saveToken_CheckBox.IsEnabled = true;
                        saveToken_CheckBox.IsChecked = false;

                        RestoreSavedCredentialsForSite(_apiBaseUrl);
                        _statusBarViewModel.OSTEventName = GetSelectedSiteEventName();
                    }
                    else
                    {
                        // No OST metadata present in event file
                        siteComboBox.IsEnabled = false;
                        siteComboBox.ItemsSource = null;
                        siteComboBox.Items.Clear();
                        _siteEntries.Clear();
                        _apiBaseUrl = string.Empty;

                        // disable authentication controls
                        loginButton.IsEnabled = false;
                        userEmail_TextBox.IsEnabled = false;
                        password_TextBox.IsEnabled = false;
                        saveToken_CheckBox.IsEnabled = false;

                        _statusBarViewModel.StatusMessage = UiStrings.Event_MissingOstMetadata;
                        return;
                    }
                }
                finally
                {
                    _suppressSitePrompt = false;
                }

                _statusBarViewModel.StatusMessage = UiStrings.Event_EventFileLoaded;
            }
            else
            {
                if (showErrors)
                    MessageBox.Show(UiStrings.Event_FailedLoadEvent);
                _statusBarViewModel.StatusMessage = UiStrings.Event_EventFileLoadFailed;
            }
        }

        private async void UploadToOSTAsync()
        {
            try
            {
                if (_authResponse == null || string.IsNullOrWhiteSpace(_authResponse.token))
                {
                    MessageBox.Show(UiStrings.Auth_AuthenticationTokenMissing);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_json))
                {
                    MessageBox.Show(UiStrings.Import_JsonDataMissing);
                    return;
                }

                await SyncSplitEntryKindsAsync();
                if (!LoadTimes(_loadedFilePath, out _json) || string.IsNullOrWhiteSpace(_json))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_splitKindSyncStatusMessage))
                {
                    _statusBarViewModel.StatusMessage = _splitKindSyncStatusMessage;
                }

                File.WriteAllText("output.json", _json);

                var apiClient = new OpenSplitTimeApiClient(_apiBaseUrl, _authResponse.token);
                var response = await apiClient.PostJsonAsync("/import", _json);

                string fileName = Path.GetFileName(_loadedFilePath);
                _statusBarViewModel.StatusMessage = UiStrings.Format(UiStrings.Upload_Completed, fileName);

                if (response != "{}")  //expect a quiet response when importing raw times
                    MessageBox.Show(UiStrings.Format(UiStrings.Upload_ApiResponse, response));
            }
            catch (Exception ex)
            {
                MessageBox.Show(UiStrings.Format(UiStrings.Upload_FailedDetails, ex.Message));
                _statusBarViewModel.StatusMessage = UiStrings.Upload_Failed;
                return;
            }
        }

        private void upload_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAuthenticated)
            {
                MessageBox.Show(UiStrings.Auth_PleaseAuthenticate);
                return;
            }

            if (!_hasLoadedTimes || string.IsNullOrWhiteSpace(_json))
            {
                MessageBox.Show(UiStrings.Import_PleaseLoadTimes);
                return;
            }

            UploadToOSTAsync();
        }

        private void password_TextBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            loginButton.IsEnabled = true;
        }

        private void userEmail_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            loginButton.IsEnabled = true;
        }
    }
}
