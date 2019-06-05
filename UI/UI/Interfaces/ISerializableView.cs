using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.UI.Interfaces
{
    public interface ISerializableView
    {
        void DeserializeView(JObject root);

        JObject SerializeView(JObject root = null);
    }
}
