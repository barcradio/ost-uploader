using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.IO.Path;

namespace ost_uploader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string AppVersion = "1.0.0";
        const string AppName = "OST Uploader";
        const string AppAuthor = "Bridgerland Amateur Radio Club";
        const string AppCopyright = "2025 Bridgerland Amateur Radio Club";
        const string AppLicense = "MIT License";
        const string AppDescription = "A simple tool to upload UltraTracker data to OpenSplitTime.org";
        const string AppRepository = "https://github.com/barcradio/ost-uploader";
        const string AppIcon = "pack://application:,,,/ost-uploader;component/Resources/ost_icon.ico";

        const string ApiBaseUrl = "https://www.opensplittime.org";
        const int TEST_EVENT_ID = 833;
        const int BEAR1002025_EVENT_ID = 995;
        private int _targetEventId = TEST_EVENT_ID;

        private APIAuthResponse? _authResponse;
        public event EventHandler? TimesLoaded;

        private bool _isAuthenticated = false;
        private string _loadedFilePath = string.Empty;
        private bool _hasLoadedTimes = false;
        private string? _json = null;


        public MainWindow()
        {
            InitializeComponent();
            this.Show();

            event_textBox.IsEnabled = false;
            station_textBox.IsEnabled = false;
            exportType_textBox.IsEnabled = false;
            csvDataGrid.IsReadOnly = true;

        }

        private async Task AuthenticateAsync(string userEmail, SecureString password)
        {
            if (string.IsNullOrWhiteSpace(userEmail) || password == null || password.Length == 0)
                return;

            _isAuthenticated = false;

            var authProvider = new AuthProvider();
            await authProvider.SignInBasicAsync(userEmail, password);

            _authResponse = authProvider.AuthResponse;

            _isAuthenticated = !string.IsNullOrWhiteSpace(_authResponse.token);
        }

        private static bool LoadTimes(string filePath, out string json)
        {
            json = null;
            var importer = new TimesImporter();
            var (header, entries) = importer.Import(filePath);
            var station = string.Empty;
            var eventname = string.Empty;
            var exportType = string.Empty;

            var stationNameMap = new StationSplitMapper();

            if (header == null)
            {
                MessageBox.Show("Failed to read header from CSV.");
                return false;
            }

            if(header.Fields.Count != 3)
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

            var formatter = new TimesJsonFormatter(AppName + "_" + fileName, stationNameMap.StationSplitMap[station]);
            json = formatter.Format(entries);

            MainWindow? instance = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

            instance?.OnTimesLoaded(EventArgs.Empty, entries, header);
            //MessageBox.Show(json);

            return true;
        }

        private async void UploadToOSTAsync()
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

            File.WriteAllText("output.json", _json);

            var apiClient = new OpenSplitTimeApiClient(ApiBaseUrl, _authResponse.token);
            var response = await apiClient.PostJsonAsync("/api/v1/event_groups/" + _targetEventId + "/import", _json);

            if (response != "{}")  //expect a quiet response when importing raw times
                MessageBox.Show($"API Response: {response}");
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

                    //MessageBox.Show($"AuthResponse Valid. \nToken Expiration: { _authResponse.expiration})";
                }
                else
                {
                    authStatus_Label.Content = "Authentication Failed";
                    authStatus_Label.ToolTip = "Login failed. Please verify OST credentials.";
                    authStatus_Label.Visibility = Visibility.Visible;
                    authStatus_Label.Foreground = Brushes.Red;
                    loginButton.IsEnabled = true;

                    //MessageBox.Show("Login failed. Please verify OST credentials.");
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
            recordsLoaded_Label.Content = $"Records Loaded: {entries.Count}";
        }

        private void browse_Button_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
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
                //MessageBox.Show($"Selected file: {_loadedFilePath}");
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
    }
}
