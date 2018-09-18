using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.Import.Garmin
{
    public class GarminImporter : IDataProvider
    {
        public event DataPointAddedToHandler DataPointAddedTo;
        public event DataPointRemovedHandler DataPointRemoved;
        public event DataStructureAddedToHandler DataStructureAddedTo;
        public event DataStructureRemovedHandler DataStructureRemoved;

        public GarminImporter()
        {
        }

        /* Open an existing gamin file */
        public async static void Open(IReadWriteSeekStreamFactory file)
        {

            int ws = 0;
            int pi = 0;
            int dc = 0;
            int cc = 0;
            int ac = 0;
            int et = 0;
            int el = 0;
            int xd = 0;

            // Import data

            // Read a document  

            XmlTextReader textReader = new XmlTextReader(await file.GetReadWriteStream());
            // Read until end of file  
            while (textReader.Read())
            {
                XmlNodeType nType = textReader.NodeType;
                // If node type us a declaration  
                if (nType == XmlNodeType.XmlDeclaration)
                {
                    Console.WriteLine("Declaration:" + textReader.Name.ToString());
                    xd = xd + 1;
                }
                // if node type is a comment  
                if (nType == XmlNodeType.Comment)
                {
                    Console.WriteLine("Comment:" + textReader.Name.ToString());
                    cc = cc + 1;
                }
                // if node type us an attribute  
                if (nType == XmlNodeType.Attribute)
                {
                    Console.WriteLine("Attribute:" + textReader.Name.ToString());
                    ac = ac + 1;
                }
                // if node type is an element  
                if (nType == XmlNodeType.Element)
                {
                    Console.WriteLine("Element:" + textReader.Name.ToString());
                    el = el + 1;
                }
                // if node type is an entity\  
                if (nType == XmlNodeType.Entity)
                {
                    Console.WriteLine("Entity:" + textReader.Name.ToString());
                    et = et + 1;
                }
                // if node type is a Process Instruction  
                if (nType == XmlNodeType.Entity)
                {
                    Console.WriteLine("Entity:" + textReader.Name.ToString());
                    pi = pi + 1;
                }
                // if node type a document  
                if (nType == XmlNodeType.DocumentType)
                {
                    Console.WriteLine("Document:" + textReader.Name.ToString());
                    dc = dc + 1;
                }
                // if node type is white space  
                if (nType == XmlNodeType.Whitespace)
                {
                    Console.WriteLine("WhiteSpace:" + textReader.Name.ToString());
                    ws = ws + 1;
                }
            }
            // Write the summary  
            Console.WriteLine("Total Comments:" + cc.ToString());
            Console.WriteLine("Total Attributes:" + ac.ToString());
            Console.WriteLine("Total Elements:" + el.ToString());
            Console.WriteLine("Total Entity:" + et.ToString());
            Console.WriteLine("Total Process Instructions:" + pi.ToString());
            Console.WriteLine("Total Declaration:" + xd.ToString());
            Console.WriteLine("Total DocumentType:" + dc.ToString());
            Console.WriteLine("Total WhiteSpaces:" + ws.ToString());




            // Check data

            // TODO: Multiple indices
            //var timeInd = Array.IndexOf<string>(columns, "Time");
            //if (timeInd < 0) throw new ArgumentException("Table does not have a column named 'Time'");

            // Create the time index
            //var timeCol = new GarminTableIndex("Time", zipEntry, archive);

            // Create the other columns
            //for (var i = 0; i < columns.Length; i++)
            //{
            //    if (i != timeInd)
            //    {
            //        this.columns.Add(new GarminTableColumn(columns[i], zipEntry, archive, timeCol));
            //    }
            //}

        }

    }

    public class GarminTable : DataStructure
    {
        public override string Name { get; set; }

        private readonly List<GarminTableColumn> columns = new List<GarminTableColumn>();

        /* Open an existing gamin file */
        internal GarminTable()
        {
            // Import data

            // Check data

            // Create index

            // Create the time index
            var timeCol = new GarminTableIndex("Time");

            // Create the other columns
            for (var i = 0; i < 1; i++)
            {
                this.columns.Add(new GarminTableColumn("Name", timeCol));
            }

        }

    }

    public class GarminTableColumn : IDataPoint
    {
        protected float[] data;
        GarminTableIndex index;
        SemaphoreSlim locker = new SemaphoreSlim(1, 1);

        internal GarminTableColumn(string name, GarminTableIndex index)
        {
            Name = name;
            this.index = index;
        }

        public Span<float> GetData(int start, int end)
        {
            return data.AsSpan(start, end - start);
        }

        public Type Type => throw new NotImplementedException();

        public string Name { get; set; }

        public async Task<IDataViewer> CreateViewerIn(DataViewerContext context)
        {
            return new GarminTableColumnViewer(index, this, context);
        }
    }

    public class GarminTableIndex : GarminTableColumn
    {
        internal GarminTableIndex(string name )
            : base(name, null)
        {

        }

        internal int findIndex(int current, double value)
        {
            // FIXME: This is far from perfect
            if (current >= 0 && data[current] == value) return current;

            // Do a binary search starting at the previous index
            int first = 0;
            int last = data.Length - 1;

            if (current < 0) current = (first + last) / 2;

            while (first < last)
            {
                if (value < data[first]) return first;
                if (value > data[last]) return last;

                if (value > data[current]) first = current + 1;
                else last = current - 1;
                current = (last + first) / 2;

            }
            return current;
        }
    }

    public class GarminTableColumnViewer : IDataViewer
    {
        GarminTableIndex time;
        GarminTableColumn column;
        DataViewerContext context;
        int startIndex = -1;
        int endIndex = -1;

        internal GarminTableColumnViewer(GarminTableIndex time, GarminTableColumn column, DataViewerContext context)
        {
            this.time = time;
            this.column = column;
            this.context = context;
            context.RangeUpdated += Context_RangeUpdated;
            Context_RangeUpdated(context.RangeFrom, context.RangeTo);
        }

        private void Context_RangeUpdated(double from, double to)
        {
            var start = time.findIndex(startIndex, from);
            var end = time.findIndex(endIndex, to);
            if (start != startIndex || end != endIndex)
            {
                startIndex = start;
                endIndex = end;
                Changed?.Invoke();
            }
        }

        public IDataPoint DataPoint { get; }

        public event DataViewWasChangedHandler Changed;

        public SpanPair<float> GetCurrentFloat()
        {
            return new SpanPair<float>(time.GetData(startIndex, endIndex), column.GetData(startIndex, endIndex));
        }

        public Span<byte> GetCurrentData()
        {
            throw new NotImplementedException();
        }
    }

}
