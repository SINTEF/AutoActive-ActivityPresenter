using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

using System.Collections.ObjectModel;
using SINTEF.AutoActive.UI.Helpers;

namespace SINTEF.AutoActive.Archive
{
    enum DataType {Video, Graph, Drawing};
    public class DummyFile: INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => this.SetProperty(ref _name, value, PropertyChanged);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class DummyArchive : INotifyPropertyChanged
    {
        private string title;
        public string Title
        {
            get => title;
            set => this.SetProperty(ref title, value, PropertyChanged);
        }
        private string path;
        public string Path
        {
            get => path;
            set => this.SetProperty(ref path, value, PropertyChanged);
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
