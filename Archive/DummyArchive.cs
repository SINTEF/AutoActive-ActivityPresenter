using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

namespace SINTEF.AutoActive.Archive
{
    public class DummyArchive : INotifyPropertyChanged
    {
        private string title;
        public string Title
        {
            get => title;
            set
            {
                title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Title"));
            }
        }
        private string path;
        public string Path
        {
            get => path;
            set
            {
                path = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Path"));
            }
        }
        public DateTime lastUsed;

        public event PropertyChangedEventHandler PropertyChanged;

        public DummyArchive(string t, string p)
        {
            title = t;
            Path = p;
        }
    }
}
