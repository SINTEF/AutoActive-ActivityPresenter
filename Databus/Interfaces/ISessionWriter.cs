using System.IO;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface ISessionWriter
    {
        string RootName { get; }
        bool JsonCreated { get; }

        string StoreMeta(JObject root);
        string StoreFile(Stream data, string name);
        void EnsureDirectory(string name);

        void PushPathName(string childName);
        void PopPathName();
    }
}
