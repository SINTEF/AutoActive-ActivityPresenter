﻿using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Implementations;

namespace SINTEF.AutoActive.Archive
{
    public abstract class ArchiveStructure : BaseDataStructure
    {
        //public override string Name { get; set; }
        // TODO: How do we handle name changes of structures in archives?
        internal void SetName(string name)
        {
            Name = name; 
        }

        public abstract string Type { get; }
        protected JObject Meta { get; private set; }
        protected JObject User { get; private set; }

        protected ArchiveStructure(JObject json)
        {
            GetUserMeta(json, out var meta, out var user);
            Meta = meta;
            User = user;
        }

        public static void GetUserMeta(JObject json, out JObject meta, out JObject user)
        {
            meta = json?.Property("meta")?.Value as JObject;
            user = json?.Property("user")?.Value as JObject;

            if (meta == null || user == null) {
                throw new ArgumentException("Object missing 'meta' or 'user' property.", nameof(json));
            }
        }

        //protected internal abstract void RegisterContents(DataStructureAddedToHandler dataStructureAdded, DataPointAddedToHandler dataPointAdded);

            /*
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
        */
    }
}
