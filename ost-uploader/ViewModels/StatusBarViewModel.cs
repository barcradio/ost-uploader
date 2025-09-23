using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ost_uploader.ViewModels
{
    public class StatusBarViewModel : INotifyPropertyChanged
    {
        private string _ostEventName;
        private string _statusMessage;

        public string OSTEventName
        {
            get => _ostEventName;
            set
            {
                if (_ostEventName != value)
                {
                    _ostEventName = value;
                    OnPropertyChanged(nameof(OSTEventName));
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}