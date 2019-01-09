using System.IO;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface ISessionWriter
    {
        string RootName { get; }
        bool JsonCreated { get; }

        void StoreMeta(JObject root);
        void StoreFile(Stream data, string name);
        void CreateDirectory(string name);

        void PushPathName(string childName);
        void PopPathName();
    }
}
