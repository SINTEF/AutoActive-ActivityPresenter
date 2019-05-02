using System.IO;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface ISessionWriter
    {
        string RootName { get; }
        bool JsonCreated { get; }

        string StoreMeta(JObject root);
        void StoreFileId(Stream data, string path);
        void PushPathName(string childName);
        void PopPathName();
    }
}
