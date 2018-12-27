using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceProcessor
{
    public class Beatmap
    {
        public string MapFile { get; set; }
        
        public int Mode { get; set; }
        
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Creator { get; set; }
        public string Version { get; set; }

        public float OD { get; set; }

        public int ID { get; set; }
        
        public int HitObjectIndex { get; set; }


        public int SR { get; set; }
        public int OldSR { get; set; }


        public List<HitObject> LoadHitObjects()
        {
            string[] mapData = System.IO.File.ReadAllLines(MapFile);

            List<HitObject> hitObjects = new List<HitObject>();

            HitObject newObject;

            for (int index = HitObjectIndex + 1; index < mapData.Length; index++)
            {
                newObject = HitObject.FromString(mapData[index]);

                if (newObject != null)
                {
                    hitObjects.Add(newObject);
                }
            }

            return hitObjects;
        }

        public static Beatmap Load(string file)
        {
            Beatmap loadMap = new Beatmap();

            loadMap.MapFile = file;

            loadMap.Mode = 0; //default to standard; empty maps will be considering "std" and ignored
            loadMap.OD = 5; //default value in case not found?
            loadMap.ID = -1; //very old maps do not have ID in file - will ignore these maps.

            string[] mapData = System.IO.File.ReadAllLines(file);

            if (mapData.Length > 0)
            {
                for (int index = 0; index < mapData.Length; index++)
                {
                    if (mapData[index].StartsWith("Mode:"))
                    {
                        loadMap.Mode = int.Parse(mapData[index].Substring(5));
                    }
                    else if (mapData[index].StartsWith("Title:"))
                    {
                        loadMap.Title = mapData[index].Substring(6);
                    }
                    else if (mapData[index].StartsWith("Artist:"))
                    {
                        loadMap.Artist = mapData[index].Substring(7);
                    }
                    else if (mapData[index].StartsWith("Creator:"))
                    {
                        loadMap.Creator = mapData[index].Substring(8);
                    }
                    else if (mapData[index].StartsWith("Version:"))
                    {
                        loadMap.Version = mapData[index].Substring(8);
                    }
                    else if (mapData[index].StartsWith("BeatmapID:"))
                    {
                        loadMap.ID = int.Parse(mapData[index].Substring(10));
                    }
                    else if (mapData[index].StartsWith("OverallDifficulty:"))
                    {
                        loadMap.OD = float.Parse(mapData[index].Substring(18));
                    }
                    else if (mapData[index].StartsWith("[HitObjects]"))
                    {
                        loadMap.HitObjectIndex = index + 1;
                        if (loadMap.Mode != 1)
                        {
                            index = mapData.Length;
                        }
                    }
                }
            }

            return loadMap;
        }

        public override string ToString()
        {
            return Title + " [" + Version + "] (" + Creator + ")";
        }
    }
}
