using System.Collections.Generic;

namespace ost_uploader
{
    public class StationSplitMapper
    {
        public List<string> SplitNames { get; }
        public List<string> StationNames { get; }
        public Dictionary<string, string> StationSplitMap { get; }

        private static readonly List<string> _splitNames = new List<string> { "Start",
                                                                              "Logan Peak",
                                                                              "Leatham Hollow",
                                                                              "Upper Richards Hollow",
                                                                              "Right Hand Fork",
                                                                              "Temple Fork",
                                                                              "Tony Grove",
                                                                              "Franklin Basin",
                                                                              "Logan River",
                                                                              "Beaver Lodge",
                                                                              "Gibson Basin",
                                                                              "Beaver Creek",
                                                                              "Ranger Dip",
                                                                              "Finish" };
        private static readonly List<string> _stationNames = new List<string> { "0-start-line",
                                                                                "1-logan-peak",
                                                                                "2-leatham-hollow",
                                                                                "3-richards-hollow",
                                                                                "4-righthand-fork",
                                                                                "5-temple-fork",
                                                                                "6-tony-grove",
                                                                                "7-franklin-trailhead",
                                                                                "8-logan-river",
                                                                                "9-beaver-mtn",
                                                                                "10-gibson-basin",
                                                                                "11-beaver-creek",
                                                                                "12-ranger-dip",
                                                                                "13-finish-line" };

        public StationSplitMapper()
        {
            SplitNames = _splitNames;
            StationNames = _stationNames;
            StationSplitMap = new Dictionary<string, string>();

            // Use index to map corresponding split and station names
            for (int i = 0; i < StationNames.Count && i < SplitNames.Count; i++)
            {
                StationSplitMap.Add(StationNames[i], SplitNames[i]);
            }
        }
    }
}



