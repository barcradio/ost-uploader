using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
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
        private int _targetEventGroup = BEAR1002025_EVENT_GROUP;
        private string _eventName = string.Empty;
        private string _statusMessage = string.Empty;

        private APIAuthResponse? _authResponse;
        public event EventHandler? TimesLoaded;

        StatusBarViewModel _statusBarViewModel;

        private bool _isAuthenticated = false;
        private SecureCredentialStore _credentialStore;
        private bool _suppressSitePrompt = false;
        private int _lastSiteIndex = -1;
        private string _loadedFilePath = string.Empty;
        private bool _hasLoadedTimes = false;
        private string? _json = null;
        private StationSplitMapper _stationNameMap = new StationSplitMapper();
        private string? _eventFilePath = null;
        private Dictionary<string, HashSet<string>> _splitEntryKindsBySplit =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private string? _splitKindSyncStatusMessage;

        public MainWindow()
        {
            InitializeComponent();
            _credentialStore = new SecureCredentialStore();
            // Initialize API base from site selector (default to Production)
            _apiBaseUrl = GetBaseUrlFromSelection();
            // record initial selection index
            _lastSiteIndex = siteComboBox?.SelectedIndex ?? -1;
            _statusBarViewModel = new StatusBarViewModel();
            this.DataContext = _statusBarViewModel;

            event_textBox.IsEnabled = false;
            station_textBox.IsEnabled = false;
            exportType_textBox.IsEnabled = false;
            csvDataGrid.IsReadOnly = true;

            recordsLoaded_Label.Content = "Records Loaded: 0";
            _statusBarViewModel.StatusMessage = "Load an event zip file to select an OST environment.";
            _statusBarViewModel.OSTEventName = $"Event: {_targetEventId}";

            // Authentication is unavailable until valid OST metadata is loaded from stations.json in an event zip.
            loginButton.IsEnabled = false;
            userEmail_TextBox.IsEnabled = false;
            password_TextBox.IsEnabled = false;
            saveToken_CheckBox.IsEnabled = false;

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
                    authStatus_Label.Content = "Authenticated";
                    authStatus_Label.ToolTip = "Token Expiration: " + _authResponse.expiration;
                    authStatus_Label.Visibility = Visibility.Visible;
                    authStatus_Label.Foreground = Brushes.Black;
                    loginButton.IsEnabled = false;
                    // Save token if user requested it
                    try
                    {
                        if (saveToken_CheckBox != null && saveToken_CheckBox.IsChecked == true)
                        {
                            _credentialStore.SaveToken(_authResponse, _apiBaseUrl);
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
                    authStatus_Label.Content = "Authentication Failed";
                    authStatus_Label.ToolTip = "Login failed. Please verify OST credentials.";
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
                _statusBarViewModel.StatusMessage = _splitKindSyncStatusMessage ?? "Ready";
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
                _splitKindSyncStatusMessage = "Ready (OpenSplitTime split-kind sync unavailable: site not selected)";
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
                    $"Ready (OpenSplitTime split-kind sync unavailable: {ex.Message})";
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
                    if (string.IsNullOrWhiteSpace(splitName) || string.IsNullOrWhiteSpace(entry.subSplitKind))
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
                var item = siteComboBox.SelectedItem as ComboBoxItem;
                if (item == null) return "";
                var tag = (item.Tag ?? string.Empty).ToString();
                return tag;
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

        private void siteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
                    var msg = "You are switching to the production OpenSplitTime environment. Any times you submit will post to the live event.";
                    var result = MessageBox.Show(msg, "Confirm Production", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes)
                    {
                        // revert selection
                        _suppressSitePrompt = true;
                        try { siteComboBox.SelectedIndex = _lastSiteIndex; }
                        finally { _suppressSitePrompt = false; }
                        return;
                    }
                }

                // Accept the change: update base URL and de-authenticate
                _apiBaseUrl = newUrl;
                _lastSiteIndex = newIndex;
                _splitEntryKindsBySplit = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                _splitKindSyncStatusMessage = null;

                if (_isAuthenticated)
                {
                    _isAuthenticated = false;
                    _authResponse = null;
                    authStatus_Label.Content = "Not Authenticated";
                    authStatus_Label.ToolTip = string.Empty;
                    authStatus_Label.Visibility = Visibility.Visible;
                    authStatus_Label.Foreground = Brushes.Red;
                    loginButton.IsEnabled = true;
                    _statusBarViewModel.OSTEventName = $"Event: {_targetEventId}";
                    _statusBarViewModel.StatusMessage = $"Waiting for authentication";
                }
            }
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
                MessageBox.Show("Authentication token is missing. Please authenticate first.");
                return;
            }
            try
            {
                var apiClient = new OpenSplitTimeApiClient(_apiBaseUrl, _authResponse.token);
                var response = await apiClient.GetAsync($"/api/v1/events/{eventId}");
                OSTEvent targetEvent = JsonSerializer.Deserialize<OSTEvent>(response);
                _statusBarViewModel.OSTEventName = targetEvent.data.attributes.name;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving event info: {ex.Message}");
            }
        }

        // Load times from CSV and format JSON for OST. Returns false on failure.
        private bool LoadTimes(string filePath, out string json)
        {
            json = null;
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
                        _statusBarViewModel.StatusMessage = "Event file auto-detected and loaded.";
                    }
                }
                catch
                {
                    // ignore event file errors and continue with defaults
                }
            }

            if (header == null)
            {
                MessageBox.Show("Failed to read header from CSV.");
                return false;
            }

            if (header.Fields.Count != 3)
            {
                MessageBox.Show("CSV header does not contain expected fields. \nEnsure file is an UltraTracker export file.");
                return false;
            }
            else
            {
                eventname = header.Fields[0];
                station = header.Fields[1];
                exportType = header.Fields[2];

                if (exportType != "full-export" && exportType != "incremental-export")
                {
                    MessageBox.Show("CSV file is not an UltraTracker export file.");
                    return false;
                }
            }

            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // Map station token to OST split name.
            string mappedSplit;
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
                    MessageBox.Show($"Station '{station}' was not found in event metadata splitNames. Please verify the event zip and CSV station identifier.");
                    return false;
                }
            }

            // API schema validation: bib numbers may include only digits or "*".
            var invalidBib = entries.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.BibId) || e.BibId.Any(c => !(char.IsDigit(c) || c == '*')));
            if (invalidBib != null)
            {
                MessageBox.Show($"Invalid bib number '{invalidBib.BibId}'. The API allows only digits 0-9 or '*'.");
                return false;
            }

            var allowedKinds = ResolveAllowedKindsForSplit(mappedSplit);
            var formatter = new TimesJsonFormatter(AppName + "_" + fileName, mappedSplit, allowedKinds);
            json = formatter.Format(entries);

            OnTimesLoaded(EventArgs.Empty, entries, header);

            return true;
        }

        protected virtual void OnTimesLoaded(EventArgs e, List<TimeEntry> entries, CsvHeader header)
        {
            TimesLoaded?.Invoke(this, e);

            event_textBox.Text = header.Fields[0];
            station_textBox.Text = header.Fields[1];
            exportType_textBox.Text = header.Fields[2];
            csvDataGrid.ItemsSource = entries;
            recordsLoaded_Label.Content = $"Records Loaded: {entries.Count}";
        }

        private void browse_Button_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Select a CSV file",
                Multiselect = false
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                _loadedFilePath = openFileDialog.FileName;

                if (!LoadTimes(_loadedFilePath, out _json))
                    return;

                _hasLoadedTimes = true;

                fileName_textBox.Text = _loadedFilePath;
                _statusBarViewModel.StatusMessage = $"Ready for Upload: {Path.GetFileName(_loadedFilePath)}";
            }
        }

        private void browseEvent_Button_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Event files (*.zip)|*.zip", 
                Title = "Select an event file",
                Multiselect = false
            };

            // Suggest common event config directory in Documents
            var docDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ultra-Tracker", ".event-config");
            if (Directory.Exists(docDir))
                openFileDialog.InitialDirectory = docDir;
            else
            {
                var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (Directory.Exists(downloads)) openFileDialog.InitialDirectory = downloads;
            }

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                LoadEventFileFromPath(openFileDialog.FileName);
            }
        }

        private void LoadEventFileFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show("Event file not found.");
                return;
            }

            if (EventFileLoader.TryLoad(path, out var data))
            {
                _eventFilePath = path;
                _stationNameMap = new StationSplitMapper(data.StationMap);
                _splitEntryKindsBySplit = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                _splitKindSyncStatusMessage = null;
                eventFile_textBox.Text = path;
                // Populate siteComboBox from event file Sites (authoritative source of OST endpoints)
                try
                {
                    _suppressSitePrompt = true;
                    siteComboBox.Items.Clear();

                    // Only enumerate connection options from openSplitTime.production and openSplitTime.staging metadata.
                    var sites = (data.Sites ?? new System.Collections.Generic.List<SiteEntry>())
                        .Where(s => string.Equals(s.Title, "Production", StringComparison.OrdinalIgnoreCase) || string.Equals(s.Title, "Staging", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (sites.Count > 0)
                    {
                        foreach (var s in sites)
                        {
                            var item = new ComboBoxItem { Content = s.Title, Tag = s.Url };
                            siteComboBox.Items.Add(item);
                        }

                        siteComboBox.IsEnabled = true;

                        // Default selection: prefer staging if present
                        int preferred = -1;
                        for (int i = 0; i < siteComboBox.Items.Count; i++)
                        {
                            var itm = siteComboBox.Items[i] as ComboBoxItem;
                            var tag = (itm?.Tag ?? string.Empty).ToString();
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

                        // Restore a saved token only when it matches the selected connection URL.
                        _isAuthenticated = false;
                        _authResponse = null;
                        try
                        {
                            if (_credentialStore.TryGetValidToken(out var saved, out var savedBaseUrl) &&
                                !string.IsNullOrWhiteSpace(savedBaseUrl) &&
                                string.Equals(savedBaseUrl, _apiBaseUrl, StringComparison.OrdinalIgnoreCase))
                            {
                                _authResponse = saved;
                                _isAuthenticated = true;
                                authStatus_Label.Content = "Authenticated (saved)";
                                authStatus_Label.ToolTip = "Token Expiration: " + _authResponse.expiration;
                                authStatus_Label.Visibility = Visibility.Visible;
                                authStatus_Label.Foreground = Brushes.Black;
                                loginButton.IsEnabled = false;
                                _statusBarViewModel.StatusMessage = "Ready";
                            }
                            else
                            {
                                authStatus_Label.Content = "Not Authenticated";
                                authStatus_Label.ToolTip = string.Empty;
                                authStatus_Label.Visibility = Visibility.Visible;
                                authStatus_Label.Foreground = Brushes.Red;
                            }
                        }
                        catch
                        {
                            authStatus_Label.Content = "Not Authenticated";
                            authStatus_Label.ToolTip = string.Empty;
                            authStatus_Label.Visibility = Visibility.Visible;
                            authStatus_Label.Foreground = Brushes.Red;
                        }
                    }
                    else
                    {
                        // No OST metadata present in event file
                        siteComboBox.IsEnabled = false;
                        siteComboBox.Items.Clear();
                        _apiBaseUrl = string.Empty;

                        // disable authentication controls
                        loginButton.IsEnabled = false;
                        userEmail_TextBox.IsEnabled = false;
                        password_TextBox.IsEnabled = false;
                        saveToken_CheckBox.IsEnabled = false;

                        _statusBarViewModel.StatusMessage = "Event file missing OST metadata — cannot authenticate.";
                        return;
                    }
                }
                finally
                {
                    _suppressSitePrompt = false;
                }

                _statusBarViewModel.StatusMessage = "Event file loaded.";
            }
            else
            {
                MessageBox.Show("Failed to load event file. The file may be malformed.");
                _statusBarViewModel.StatusMessage = "Event file load failed.";
            }
        }

        private async void UploadToOSTAsync()
        {
            try
            {
                if (_authResponse == null || string.IsNullOrWhiteSpace(_authResponse.token))
                {
                    MessageBox.Show("Authentication token is missing. Please authenticate first.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_json))
                {
                    MessageBox.Show("JSON data is missing. Please load times from a CSV file first.");
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
                _statusBarViewModel.StatusMessage = $"Upload completed: {fileName}";

                if (response != "{}")  //expect a quiet response when importing raw times
                    MessageBox.Show($"API Response: {response}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Upload failed: {ex.Message}");
                _statusBarViewModel.StatusMessage = "Upload failed.";
                return;
            }
        }

        private void upload_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAuthenticated)
            {
                MessageBox.Show("Please authenticate before uploading.");
                return;
            }

            if (!_hasLoadedTimes || string.IsNullOrWhiteSpace(_json))
            {
                MessageBox.Show("Please load times from a CSV file before uploading.");
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
