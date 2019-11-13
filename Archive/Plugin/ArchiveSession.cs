using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Archive.Plugin
{
    public class BasedOnInfo
    {
        public Guid id;
        public string name;
        public DateTimeOffset created;
        public string archive_filename;

        public BasedOnInfo(ArchiveSession session)
        {
            id = session.Id;
            name = session.Name;
            created = session.Created;
            archive_filename = session.GetArchiveFilename();
        }
    }

    public class ArchiveSession : ArchiveFolder, IDataProvider
    {
        public override string Type => "no.sintef.session";

        private readonly Archive _archive;
        public Guid Id { get; }
        private List<BasedOnInfo> _basedOn = new List<BasedOnInfo>();

        public DateTimeOffset Created { get; }
        public const string SessionFileName = "AUTOACTIVE_SESSION.json";

        internal ArchiveSession(JObject json, Archive archive, Guid sessionId) : base(json, archive, sessionId)
        {
            var id = Meta["id"].ToObject<Guid?>();
            var name = User["name"].ToObject<string>();
            var created = User["created"].ToObject<DateTimeOffset?>();
            
            Id = id ?? throw new ArgumentException("Session is missing 'id'");
            Name = name ?? throw new ArgumentException("Session is missing 'name'");
            Created = created ?? throw new ArgumentException("Session is missing 'created'");
            IsSaved = true;
            _archive = archive;
        }

        public static ArchiveSession Create(Archive archive, string name, List<BasedOnInfo> basedOn)
        {
            Guid sessionId = Guid.NewGuid();
            var meta = new JObject { ["id"] = sessionId, ["based_on"] = JToken.FromObject(basedOn) };
            var user = new JObject { ["name"] = name, ["created"] = DateTimeOffset.Now };
            var json = new JObject { ["meta"] = meta, ["user"] = user };

            return new ArchiveSession(json, archive, sessionId) { IsSaved = false, _basedOn = basedOn };
        }

        public static ArchiveSession Create(Archive archive, string name, ArchiveSession basedOnSession)
        {
            var basedOn = new List<BasedOnInfo>();
            if (basedOnSession != null) basedOn.Add(new BasedOnInfo(basedOnSession));

            return Create(archive, name, basedOn);
        }

        public static ArchiveSession Create(Archive archive, string name)
        {
            return Create(archive, name, new List<BasedOnInfo>());
        }

        public void AddBasedOnSession(ArchiveSession basedOnSession)
        {
            _basedOn.Add(new BasedOnInfo(basedOnSession));  // TODO check for session duplicates
            Meta["based_on"] = JToken.FromObject(_basedOn); // Refresh metadata
        }

        public string GetArchiveFilename()
        {
            return _archive.GetFilename();
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public void Close()
        {
            _archive.Close();
        }

        public static async Task WriteChildren(ISessionWriter sessionWriter, JObject json, IEnumerable<IDataStructure> children)
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
                    await WriteChildren(sessionWriter, root, child.Children);
                    sessionWriter.PopPathName();
                }

                JObject user;
                if (!json.TryGetValue("user", out var currentUser))
                {
                    // No 'user' obj in json ... make a default and use it
                    user = new JObject();
                }

                if (currentUser is JObject o)
                {
                    // Found 'user' obj in json ... use it
                    user = o;
                }
                else
                {
                    // Found 'user' obj in json, but not a JObject ... make a default and use it
                    user = new JObject();
                }

                if (child.Name != null)
                {
                    // Store tree from child
                    user[child.Name] = root;
                }
                else
                {
                    // Merge tree
                    user.Merge(root);
                }

                // Update json
                json["user"] = user;
            }
        }

        public async Task WriteFile(ZipFile zipFile)
        {
            var sessionWriter = new ArchiveSessionWriter(zipFile, this);

            // Use begin/commit close to the actual changes    sessionWriter.BeginUpdate();

            var sessionJsonRoot = ToArchiveJson();
            await WriteChildren(sessionWriter, sessionJsonRoot, Children);

            // Use begin/commit close to the actual changes    sessionWriter.CommitUpdate();
            sessionWriter.StoreMeta(sessionJsonRoot);

        }

        public override async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            await base.WriteData(root, writer);

            // FIXME This is never called during save ....

            // Copy previous session selectivly
            root["meta"]["type"] = Type;
            root["meta"]["id"] = Id.ToString();
            root["meta"]["based_on"] = new JObject(_basedOn); 
            root["meta"]["version"] = Meta["version"];

            root["user"]["created"] = Created.ToString();
            root["user"]["name"] = User["name"];

            return true;
        }
    }


    [ArchivePlugin("no.sintef.session")]
    public class ArchiveSessionPlugin : IArchivePlugin
    {
        public Task<ArchiveStructure> CreateFromJSON(JObject json, Archive archive, Guid sessionId)
        {
            return Task.FromResult<ArchiveStructure>(new ArchiveSession(json, archive, sessionId));
        }
    }
}
