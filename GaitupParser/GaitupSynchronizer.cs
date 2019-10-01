using System;
using System.Collections.Generic;
using System.Linq;

namespace GaitupParser
{
    public class GaitupSynchronizer
    {
        private GaitupData _master;
        private readonly List<GaitupData> _slaves = new List<GaitupData>();

        public GaitupSynchronizer(IReadOnlyList<GaitupData> dataSets)
        {
            AddDataSets(dataSets);
        }

        private static long CalcOffset(GaitupData slave)
        {
            if (slave.Radio.Count == 0)
                return 0;
            var (masterTime, slaveTime, _) = slave.Radio.First();
            var offset = slaveTime - masterTime;
            return offset;
        }

        public void Synchronize(GaitupData slave, long startTime)
        {
            if(_master == slave) throw new InvalidOperationException("Can't synchronize with master");
            if (slave.Radio.Count == 0) throw new InvalidOperationException("No radio messages recorded - can't synchronize");

            slave.OffsetTime(CalcOffset(slave) - startTime);
        }

        public void AddSlave(GaitupData slave)
        {
            if (slave.Config.Radio.Mode == 0)
                throw new InvalidOperationException("The slave should not have mode = 0");
            if (slave.Config.Radio.Channel != _master.Config.Radio.Channel)
                throw new InvalidOperationException("The data sets is not from the same session as the master.");
            if (slave.Config.Frequency != _master.Config.Frequency)
                throw new InvalidOperationException("The data sets must have the same frequency.");

            _slaves.Add(slave);
        }

        public void AddDataSets(IReadOnlyList<GaitupData> dataSets)
        {
            if (dataSets.Count == 1)
            {
                _master = dataSets.First();
            }
            else
            {
                try
                {
                    _master = dataSets.Single(el => el.Config.Radio.Mode == 0);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException("Cannot find any master with mode = 0");
                }
            }

            // All except the master are slaves
            foreach (var slave in dataSets.Where(el => el != _master))
            {
                AddSlave(slave);
            }
        }

        public void Synchronize(bool doCrop = true)
        {
            long startTime = 0;
            startTime = _master.MinTime;

            foreach (var slave in _slaves)
            {
                Synchronize(slave, startTime);
            }

            _master.OffsetTime(-startTime);

            if (doCrop)
            {
                CropSets();
            }
        }

        public void CropSets()
        {
            var dataSets = new List<GaitupData>(_slaves) {_master};
            var endTime = dataSets.Min(ds => ds.MaxTime);
            foreach (var dataSet in dataSets)
            {
                dataSet.Crop(0, endTime);
            }
        }
    }
}
