using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Archive.Plugin
{
    public class ArchiveSession : ArchiveFolder, IDataProvider
    {
        public override string Type => "no.sintef.session";

        public Guid Id { get; }
        public DateTimeOffset Created { get; }
        public const string SessionFileName = "AUTOACTIVE_SESSION.json";

        internal ArchiveSession(JObject json, Archive archive) : base(json, archive)
        {
            var id = Meta["id"].ToObject<Guid?>();
            var name = User["name"].ToObject<string>();
            var created = User["created"].ToObject<DateTimeOffset?>();
            
            Id = id ?? throw new ArgumentException("Session is missing 'id'");
            Name = name ?? throw new ArgumentException("Session is missing 'name'");
            Created = created ?? throw new ArgumentException("Session is missing 'created'");
            IsSaved = true;
        }

        public static ArchiveSession Create(Archive archive, string name)
        {
            var meta = new JObject {["id"] = Guid.NewGuid()};
            var user = new JObject {["name"] = name, ["created"] = DateTimeOffset.Now};
            var json = new JObject {["meta"] = meta, ["user"] = user};

            return new ArchiveSession(json, archive) { IsSaved = false };
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        // FIXME: Implement these
        public event DataStructureAddedHandler DataStructureAddedToTree;
        public event DataStructureRemovedHandler DataStructureRemovedFromTree;
        public event DataPointAddedHandler DataPointAddedToTree;
        public event DataPointRemovedHandler DataPointRemovedFromTree;

        public static async void WriteChildren(ISessionWriter sessionWriter, JObject json, IEnumerable<IDataStructure> children)
        {
            foreach (var child in children)
            {
                var root = new JObject();
                try
                {
                    if (!(child is ISaveable saveable))
                    {
                        Debug.WriteLine($"Could not save {child.Name} ({child} not saveable)");
                        continue;
                    }

                    if (!await saveable.WriteData(root, sessionWriter))
                    {
                        Debug.WriteLine($"Could not save {child.Name} (save failed)");
                        continue;
                    }
                }
                catch (NotImplementedException)
                {
                    Debug.WriteLine($"Could not save {child.Name} (save not implemented)");
                    continue;
                }

                if (child.Children.Any())
                {
                    sessionWriter.PushPathName(child.Name);
                    WriteChildren(sessionWriter, root, child.Children);
                    sessionWriter.PopPathName();
                }

                JObject user;
                if (!json.TryGetValue("user", out var currentUser))
                {
                    user = new JObject();
                    json["user"] = user;
                }

                if (currentUser is JObject o)
                {
                    user = o;
                }
                else
                {
                    user = new JObject();
                    json["user"] = user;
                }

                if (child.Name != null)
                {
                    user[child.Name] = root;
                }
                else
                {
                    user.Merge(root);
                }
            }
        }

        public void WriteFile(ZipFile zipFile)
        {
            var sessionWriter = new ArchiveSessionWriter(zipFile, this);
            var sessionJsonRoot = ToArchiveJson();

            WriteChildren(sessionWriter, sessionJsonRoot, Children);

            sessionWriter.StoreMeta(sessionJsonRoot);
        }
    }


    [ArchivePlugin("no.sintef.session")]
    public class ArchiveSessionPlugin : IArchivePlugin
    {
        public Task<ArchiveStructure> CreateFromJSON(JObject json, Archive archive)
        {
            return Task.FromResult<ArchiveStructure>(new ArchiveSession(json, archive));
        }
    }
}
