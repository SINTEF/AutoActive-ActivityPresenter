using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.Databus;

namespace SINTEF.AutoActive.Archive
{
    public abstract class ArchiveStructure : DataStructure
    {
        public override string Name { get; set; }

        public abstract string Type { get; }
        protected JObject Meta { get; private set; }
        protected JObject User { get; private set; }

        protected ArchiveStructure(JObject json)
        {
            if (TryGetUserMeta(json, out var meta, out var user))
            {
                Meta = meta;
                User = user;
            }
            else
            {
                throw new ArgumentException("Object missing 'meta' or 'user' property.", nameof(json));
            }
        }

        private static bool TryGetUserMeta(JObject json, out JObject meta, out JObject user)
        {
            meta = json?.Property("meta")?.Value as JObject;
            user = json?.Property("user")?.Value as JObject;
            return meta != null && user != null;
        }

        protected internal abstract void RegisterContents(DataStructureAddedToHandler dataStructureAdded, DataPointAddedToHandler dataPointAdded);

        protected abstract void ToArchiveJSON(JObject meta, JObject user);

        protected JObject ToArchiveJSON()
        {
            var json = new JObject();
            var meta = new JObject();
            var user = new JObject();
            ToArchiveJSON(meta, user);
            meta["type"] = Type;
            json["meta"] = meta;
            json["user"] = user;
            return json;
        }
    }
}
