using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ost_uploader
{
    public class OSTEvent
    {
        public Data data { get; set; }
    }

    public class Data
    {
        public string id { get; set; }
        public string type { get; set; }
        public Attributes attributes { get; set; }
        public Relationships relationships { get; set; }
        public Links links { get; set; }
    }

    public class Attributes
    {
        public int id { get; set; }
        public int courseId { get; set; }
        public int organizationId { get; set; }
        public string name { get; set; }
        public DateTime startTime { get; set; }
        public DateTime scheduledStartTime { get; set; }
        public string homeTimeZone { get; set; }
        public DateTime startTimeLocal { get; set; }
        public DateTime startTimeInHomeZone { get; set; }
        public DateTime scheduledStartTimeLocal { get; set; }
        public bool concealed { get; set; }
        public int lapsRequired { get; set; }
        public int maximumLaps { get; set; }
        public bool multiLap { get; set; }
        public string slug { get; set; }
        public object shortName { get; set; }
        public bool multipleSubSplits { get; set; }
        public string[] parameterizedSplitNames { get; set; }
        public string[] splitNames { get; set; }
    }

    public class Relationships
    {
        public Efforts efforts { get; set; }
        public Splits splits { get; set; }
        public Aidstations aidStations { get; set; }
        public Course course { get; set; }
        public Eventgroup eventGroup { get; set; }
    }

    public class Efforts
    {
        public object[] data { get; set; }
    }

    public class Splits
    {
        public Datum[] data { get; set; }
    }

    public class Datum
    {
        public string id { get; set; }
        public string type { get; set; }
    }

    public class Aidstations
    {
        public Datum1[] data { get; set; }
    }

    public class Datum1
    {
        public string id { get; set; }
        public string type { get; set; }
    }

    public class Course
    {
        public Data1 data { get; set; }
    }

    public class Data1
    {
        public string id { get; set; }
        public string type { get; set; }
    }

    public class Eventgroup
    {
        public Data2 data { get; set; }
    }

    public class Data2
    {
        public string id { get; set; }
        public string type { get; set; }
    }

    public class Links
    {
        public string self { get; set; }
    }
}
