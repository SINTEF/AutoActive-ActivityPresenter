using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;


namespace SINTEF.AutoActive.UI.Pages
{
    public class DataGroups
    {
        private static List<DataGroup> _groups = new List<DataGroup>();

        public static List<DataGroup> Groups
        {
            get => _groups;
        }

        public static List<DataGroup> GetDataGroups()
        {
            return _groups;
        }

        public static void AddGroup(DataGroup group)
        {
            _groups.Add(group);
        }

        public static void DeleteGroup(DataGroup group)
        {
            _groups.Remove(group);
        }

        public static List<string> GroupNames()
        {
            List<string> names = new List<string>();

            foreach (DataGroup group in _groups)
            {
                names.Add(group.GroupName);
            }

            return names;
        }

        public static DataGroup DataGroupContainingDatapoint(IDataPoint dataPoint)
        {
            DataGroup groupOfInterest = null;
            foreach (DataGroup group in _groups)
            {
                if (group.ContainsDatapoint(dataPoint) == true)
                {
                    groupOfInterest = group;
                    break;
                }
            }
            return groupOfInterest;
        }

        public static DataGroup SearchForDataGroupByName(string Name)
        {
            foreach (DataGroup group in _groups)
            {
                if (group.GroupName == Name)
                {
                    return group;
                }
            }
            return null;
        }

        public static void AddOffsetToGroup(DataGroup group, long offset)
        {
            group.Offset = offset;
        }

        public static long GetOffsetForGroup(IDataPoint dataPoint)
        {
            DataGroup group = DataGroupContainingDatapoint(dataPoint);
            return group.Offset;

        }

        public static void AddDataPointsToGroup(List<IDataPoint> dataPoints, DataGroup group)
        {
            foreach (IDataPoint dataPoint in dataPoints)
            {
                group.AddDataPoint(dataPoint);
            }
        }

        public static void SaveOffsetToGroup(DataGroup group)
        {
            group.SaveOffsetForGroup();
        }

    }

    public class DataGroup
    {
        private string _groupName;
        public string GroupName
        {
            get => _groupName;
        }

        private long _offset = 0;
        internal long Offset
        {
            get => _offset;
            set => _offset = value;
        }

        private List<IDataPoint> group = new List<IDataPoint>();
        public DataGroup(string name)
        {
            _groupName = name;
        }

        internal void AddDataPoint(IDataPoint dataPoint)
        {
            group.Add(dataPoint);
        }

        internal bool ContainsDatapoint(IDataPoint dataPoint)
        {
            foreach (IDataPoint groupDataPoint in group)
            {
                if (groupDataPoint == dataPoint)
                {
                    return true;
                }
            }

            return false;
        }

        internal void SaveOffsetForGroup()
        {
            List<ITimePoint> timePoints = GetUniqueTimePoints();
            foreach (ITimePoint timePoint in timePoints)
            {
                timePoint.TransformTime(_offset, 1);
            }
        }

        private List<ITimePoint> GetUniqueTimePoints()
        {
            List<ITimePoint> uniqueTimePoints = new List<ITimePoint>();
            foreach (IDataPoint dataPoint in group)
            {
                if (uniqueTimePoints.Find(x => x == dataPoint.Time) == null)
                {
                    uniqueTimePoints.Add(dataPoint.Time);
                }
            }
            return uniqueTimePoints;
        }


    }
}
