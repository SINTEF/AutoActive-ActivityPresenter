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
}
