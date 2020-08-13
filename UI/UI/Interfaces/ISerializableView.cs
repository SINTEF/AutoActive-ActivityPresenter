using System.Runtime.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.UI.Interfaces
{
    public interface ISerializableView
    {
        string ViewType { get; }
        Task DeserializeView(JObject root, IDataStructure archive=null);

        JObject SerializeView(JObject root = null);
    }

    public static class SerializableViewHelper
    {
        public static bool EnsureViewType(JObject root, ISerializableView view, bool throwException = true)
        {
            var viewType = root["type"].Value<string>();
            if (viewType == view.ViewType) return true;

            if (throwException)
            {
                throw new SerializationException("Could not serialize view - wrong view type detected.");
            }
            return false;
        }
        public static JObject SerializeDefaults(JObject root, ISerializableView view)
        {
            if(root == null) root = new JObject();
            root["type"] = view.ViewType;
            return root;
        }
    }
}
