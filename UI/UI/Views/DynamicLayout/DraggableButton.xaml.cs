using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.Views.TreeView;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views.DynamicLayout
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class DraggableButton : ContentView, IDraggable
    {
        public DraggableButton()
        {
            InitializeComponent();
        }

        public DataTreeView ParentTree { get; set; }

        public void OnButtonClicked(object sender, EventArgs args)
        {

        }
    }
}