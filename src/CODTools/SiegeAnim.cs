using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CODTools
{
    public class Animation
    {
        public int frames { get; set; }
        public int loop { get; set; }
        public int nodes { get; set; }
        public int playbackSpeed { get; set; }
        public int speed { get; set; }
    }

    public class DataPositions
    {
        public int byteSize { get; set; }
        public int byteStride { get; set; }
    }

    public class DataQuaternions
    {
        public int byteSize { get; set; }
        public int byteStride { get; set; }
    }

    public class Data
    {
        public DataPositions dataPositionsBase { get; set; }
        public DataQuaternions dataRotationsBase { get; set; }

        public Data()
        {
            this.dataPositionsBase = new DataPositions();
            this.dataRotationsBase = new DataQuaternions();
        }
    }

    public class Info
    {
        public string argJson { get; set; }
        public string computer { get; set; }
        public string domain { get; set; }
        public string ta_game_path { get; set; }
        public string time { get; set; }
        public string user { get; set; }
    }

    public class Node
    {
        public string name { get; set; }
    }

    public class Shot
    {
        public int end { get; set; }
        public string name { get; set; }
        public int start { get; set; }
    }

    public class SiegeAnim
    {
        public Animation animation { get; set; }
        public Data data { get; set; }
        public Info info { get; set; }
        public List<Node> nodes { get; set; }
        public List<Shot> shots { get; set; }

        public SiegeAnim()
        {
            this.animation = new Animation();
            this.data = new Data();
            this.info = new Info();
            this.nodes = new List<Node>();
            this.shots = new List<Shot>();
        }
    }
}
