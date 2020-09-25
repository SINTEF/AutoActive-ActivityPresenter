using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

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

        private static DataGroup DataGroupContainingDatapoint(IDataPoint dataPoint)
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

        private static DataGroup SearchForDataGroupByName(string Name)
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

        public static void AddOffsetToGroup(IDataPoint dataPoint, long offset)
        {
            DataGroup group = DataGroupContainingDatapoint(dataPoint);
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
            foreach (IDataPoint dataPoint in group)
            {
                dataPoint.Time.TransformTime(Offset, 1);
            }
        }


    }
}
