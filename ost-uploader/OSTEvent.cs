using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ost_uploader
{
    public class OSTEvent
    {
        public Data data { get; set; } = null!;
    }

    public class OSTEventGroup
    {
        public EventGroupData data { get; set; } = null!;
    }

    public class Data
    {
        public string id { get; set; } = null!;
        public string type { get; set; } = null!;
        public Attributes attributes { get; set; } = null!;
        public Relationships relationships { get; set; } = null!;
        public Links links { get; set; } = null!;
    }

    public class EventGroupData
    {
        public string id { get; set; } = null!;
        public string type { get; set; } = null!;
        public EventGroupAttributes attributes { get; set; } = null!;
        public Links links { get; set; } = null!;
    }

    public class Attributes
    {
        public int id { get; set; }
        public int courseId { get; set; }
        public int organizationId { get; set; }
        public string name { get; set; } = null!;
        public DateTime startTime { get; set; }
        public DateTime scheduledStartTime { get; set; }
        public string homeTimeZone { get; set; } = null!;
        public DateTime startTimeLocal { get; set; }
        public DateTime startTimeInHomeZone { get; set; }
        public DateTime scheduledStartTimeLocal { get; set; }
        public bool concealed { get; set; }
        public int lapsRequired { get; set; }
        public int maximumLaps { get; set; }
        public bool multiLap { get; set; }
        public string slug { get; set; } = null!;
        public object shortName { get; set; } = null!;
        public bool multipleSubSplits { get; set; }
        public string[] parameterizedSplitNames { get; set; } = Array.Empty<string>();
        public string[] splitNames { get; set; } = Array.Empty<string>();
    }

    public class EventGroupAttributes
    {
        public int id { get; set; }
        public List<DataEntryGroup> dataEntryGroups { get; set; } = new();
        public List<DataEntryGroup> unpairedDataEntryGroups { get; set; } = new();
    }

    public class DataEntryGroup
    {
        public List<DataEntryGroupEntry> entries { get; set; } = new();
    }

    public class DataEntryGroupEntry
    {
        public string splitName { get; set; } = null!;
        public string subSplitKind { get; set; } = null!;
    }

    public class Relationships
    {
        public Efforts efforts { get; set; } = null!;
        public Splits splits { get; set; } = null!;
        public Aidstations aidStations { get; set; } = null!;
        public Course course { get; set; } = null!;
        public Eventgroup eventGroup { get; set; } = null!;
    }

    public class Efforts
    {
        public object[] data { get; set; } = Array.Empty<object>();
    }

    public class Splits
    {
        public Datum[] data { get; set; } = Array.Empty<Datum>();
    }

    public class Datum
    {
        public string id { get; set; } = null!;
        public string type { get; set; } = null!;
    }

    public class Aidstations
    {
        public Datum1[] data { get; set; } = Array.Empty<Datum1>();
    }

    public class Datum1
    {
        public string id { get; set; } = null!;
        public string type { get; set; } = null!;
    }

    public class Course
    {
        public Data1 data { get; set; } = null!;
    }

    public class Data1
    {
        public string id { get; set; } = null!;
        public string type { get; set; } = null!;
    }

    public class Eventgroup
    {
        public Data2 data { get; set; } = null!;
    }

    public class Data2
    {
        public string id { get; set; } = null!;
        public string type { get; set; } = null!;
    }

    public class Links
    {
        public string self { get; set; } = null!;
    }
}
