using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ost_uploader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            RunAsync();
        }

        private async void RunAsync()
        {
            var importer = new TimesImporter();
            var (header, entries) = importer.Import(@".\resources\Aid05Times.csv");

            var stationNameMap = new StationSplitMapper();

            if (header == null)
            {
                MessageBox.Show("Failed to read header from CSV.");
                Application.Current.Shutdown();
                return;
            }
            
            var eventname = header.Fields[0];
            var location = header.Fields[1];
            var exportType = header.Fields[2];

            var formatter = new TimesJsonFormatter(eventname + "_" + location + "_" + exportType, stationNameMap.StationSplitMap[location]);
            var json = formatter.Format(entries);
            //MessageBox.Show(json);

            var userEmail = ""; // Replace with actual email inout
            var password = ""; // Replace with actual password input, securely handled

            // Example of using AuthProvider to sign in
            var authProvider = new AuthProvider();
            authProvider.SignInBasicAsync(userEmail, password).Wait();
            var authResponse = authProvider.AuthResponse;
            //MessageBox.Show($"Token: {authResponse.token}\nExpiresAt: {authResponse.expiration}");
            
            // Now you can use authResponse.Token for authenticated API requests
            var apiClient = new OpenSplitTimeApiClient("https://www.opensplittime.org", authResponse.token);
            var response = await apiClient.PostJsonAsync("/api/v1/event_groups/833/import", json);
            //var response = await apiClient.GetAsync("/api/v1/events/test-event-2025");
            MessageBox.Show($"API Response: {response}");

            File.WriteAllText("output.json", json);
            Application.Current.Shutdown();
        }
    }
     
}