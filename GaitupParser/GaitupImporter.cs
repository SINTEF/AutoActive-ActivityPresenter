using System.Collections.Generic;
using System.IO;

namespace GaitupParser
{
    public class GaitupImporter
    {
        public static List<GaitupData> ImportGaitup(List<Stream> files)
        {
            var data = new List<GaitupData>();

            foreach (var file in files)
            {
                var parser = new GaitupParser(file);
                parser.ParseFile();
                data.Add(parser.GetData());
            }

            var synchronizer = new GaitupSynchronizer(data);
            synchronizer.Synchronize();
            synchronizer.CropSets();

            return data;
        }
    }
}
