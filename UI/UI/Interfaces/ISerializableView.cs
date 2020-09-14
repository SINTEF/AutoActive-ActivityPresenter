using System.Runtime.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.UI.Interfaces
{
    public interface ISerializableView
    {
        /// <summary>
        /// A string that identifies the View. Should be in the Java-style format.
        /// </summary>
        string ViewType { get; }

        /// <summary>
        /// Deserialize the provided JSON and optionally an archive.
        /// </summary>
        /// <param name="root">JSON-serialized view</param>
        /// <param name="archive">An optional archive (IDataStructure) that the data points should be selected from. If this is not provided, the data is searched for through all open archives.</param>
        /// <returns>An awaitable task</returns>
        Task DeserializeView(JObject root, IDataStructure archive=null);

        /// <summary>
        /// Copy the important parts of the view to the JSON file
        /// </summary>
        /// <param name="root">If provided, the view should be written into this JSON base</param>
        /// <returns>A JSON-serialized version of the view</returns>
        JObject SerializeView(JObject root = null);
    }

    public static class SerializableViewHelper
    {
        public static string Version = "1.0.0";
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
            if (root == null) root = new JObject();
            if (!root.ContainsKey("type"))
                root["type"] = view.ViewType;
            return root;
        }
    }
}
