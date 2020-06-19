using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.UI.Interfaces
{
    public interface ISerializableView
    {
        string ViewType { get; }
        Task DeserializeView(JObject root);

        JObject SerializeView(JObject root = null);
    }
}
