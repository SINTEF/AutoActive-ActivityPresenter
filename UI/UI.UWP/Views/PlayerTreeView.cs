using Windows.UI.Xaml.Controls;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.UWP.Views;
using SINTEF.AutoActive.UI.Views.TreeView;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(PlayerTreeView), typeof(TreeViewRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    class TreeViewRenderer : ViewRenderer<PlayerTreeView, Canvas>, IDropCollector
    {
        public PlayerTreeView PlayerTreeView;
        protected override void OnElementChanged(ElementChangedEventArgs<PlayerTreeView> e)
        {
            base.OnElementChanged(e);

            PlayerTreeView = e.NewElement;
        }

        public void ObjectDroppedOn(IDraggable item)
        {
            PlayerTreeView?.ObjectDroppedOn(item);
        }
    }
}


