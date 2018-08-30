using System;
using System.Collections.Generic;
using System.Text;

using System.Diagnostics;
using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.Archive.Plugin;

[assembly: ArchivePlugin(typeof(ArchiveSessionPlugin), "no.sintef.session")]
namespace SINTEF.AutoActive.Archive.Plugin
{
    public class ArchiveSession : ArchiveFolder
    {
        public override string Type => "no.sintef.session";

        public Guid Id { get; private set; }
        public DateTimeOffset Created { get; private set; }

        internal ArchiveSession(JObject json, Archive archive) : base(json, archive)
        {
            var id = Meta["id"].ToObject<Guid?>();
            var name = User["Name"].ToObject<string>();
            var created = User["Created"].ToObject<DateTimeOffset?>();
            
            Id = id ?? throw new ArgumentException("Session is missing 'id'");
            Name = name ?? throw new ArgumentException("Session is missing 'name'");
            Created = created ?? throw new ArgumentException("Session is missing 'created'");
        }

        protected override void ToArchiveJSON(JObject meta, JObject user)
        {
            // FIXME: Implement this!
            base.ToArchiveJSON(meta, user);
        }
    }

    public class ArchiveSessionPlugin : IArchivePlugin
    {
        public ArchiveStructure CreateFromJSON(JObject json, Archive archive)
        {
            return new ArchiveSession(json, archive);
        }
    }
}
