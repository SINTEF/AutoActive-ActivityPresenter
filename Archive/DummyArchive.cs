using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

using System.Collections.ObjectModel;

namespace SINTEF.AutoActive.Archive
{
    enum DataType {Video, Graph, Drawing};
    public class DummyFile: INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

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

        private ObservableCollection<DummyFile> _files;
        public ObservableCollection<DummyFile> Files
        {
            get => _files;
        }
        public DateTime lastUsed;

        public event PropertyChangedEventHandler PropertyChanged;

        public DummyArchive(string t, string p, string[] files)
        {
            title = t;
            Path = p;

            _files = new ObservableCollection<DummyFile>();

            foreach (var fname in files) _files.Add(new DummyFile { Name = fname });
        }

        public void AddFile(string name)
        {
            _files.Add(new DummyFile { Name = name });
        }
    }
}
