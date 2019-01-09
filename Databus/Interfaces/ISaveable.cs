using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface ISaveable
    {
        bool IsSaved { get; }

        Task<bool> WriteData(JObject root, ISessionWriter writer);
    }
}
