using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Archive.Plugin
{
    public class ArchiveSession : ArchiveFolder, IDataProvider
    {
        public override string Type => "no.sintef.session";

        public Guid Id { get; private set; }
        public DateTimeOffset Created { get; private set; }

        internal ArchiveSession(JObject json, Archive archive) : base(json, archive)
        {
            var id = Meta["id"].ToObject<Guid?>();
            var name = User["name"].ToObject<string>();
            var created = User["created"].ToObject<DateTimeOffset?>();
            
            Id = id ?? throw new ArgumentException("Session is missing 'id'");
            Name = name ?? throw new ArgumentException("Session is missing 'name'");
            Created = created ?? throw new ArgumentException("Session is missing 'created'");
        }

        // FIXME: Implement these
        public event DataStructureAddedHandler DataStructureAddedToTree;
        public event DataStructureRemovedHandler DataStructureRemovedFromTree;
        public event DataPointAddedHandler DataPointAddedToTree;
        public event DataPointRemovedHandler DataPointRemovedFromTree;

        /*
        protected override void ToArchiveJSON(JObject meta, JObject user)
        {
            // FIXME: Implement this!
            base.ToArchiveJSON(meta, user);
        }
        */
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
