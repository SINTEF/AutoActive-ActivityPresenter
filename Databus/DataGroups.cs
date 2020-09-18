using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Databus
{
    class DataGroups
    {
        static List<DataGroup> Groups = new List<DataGroup>();

        public void AddGroup(DataGroup group)
        {
            Groups.Add(group);
        }

    }

    class DataGroup
    {
        List<IDataPoint> Group = new List<IDataPoint>();
        long offset = 0;
        public DataGroup()
        {

        }





    }
}
