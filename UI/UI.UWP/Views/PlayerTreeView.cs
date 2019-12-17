using Windows.UI.Xaml.Controls;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.UWP.Views;
using SINTEF.AutoActive.UI.Views.TreeView;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(DataTreeView), typeof(TreeViewRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    class TreeViewRenderer : ViewRenderer<DataTreeView, Canvas>, IDropCollector
    {
        public DataTreeView DataTreeView;
        protected override void OnElementChanged(ElementChangedEventArgs<DataTreeView> e)
        {
            base.OnElementChanged(e);

            DataTreeView = e.NewElement;
        }

        public void ObjectDroppedOn(IDraggable item)
        {
            DataTreeView?.ObjectDroppedOn(item);
        }
    }
}


