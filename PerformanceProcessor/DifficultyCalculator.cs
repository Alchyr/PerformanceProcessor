using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceProcessor
{
    public class DifficultyCalculator
    {
        static readonly int[] Mods =
        {
            1, //0 nf
            2, //1 ez
            4, //2 touchscreen?
            8, //3 hd
            16, //4 hr
            32, //5 sd
            64, //6 dt
            128, //7 rx
            256, //8 ht
            512, //9 Nightcore
            1024, //10 fl
            2048, //11 at
            4096, //12 so
            8192, //13 ap
            16384 //14 Perfect
        };
        static readonly string[] ModStrings =
        {
            "NF",
            "EZ",
            "", //touchscreen detected ?
            "HD",
            "HR",
            "SD",
            "DT",
            "RX",
            "HT",
            "NC", //nightcore should override dt
            "FL",
            "AT",
            "SO",
            "AP",
            "PF" //perfect should override sd
        };

        // These values are results of tweaking a lot.

        private const double star_scaling_factor = 0.04075;

        /// <summary>
        /// In milliseconds. For difficulty calculation we will only look at the highest strain value in each time interval of size STRAIN_STEP.
        /// This is to eliminate higher influence of stream over aim by simply having more HitObjects with high strain.
        /// The higher this value, the less strains there will be, indirectly giving long beatmaps an advantage.
        /// </summary>
        private const double strain_step = 200.0;

        /// <summary>
        /// The weighting of each strain value decays to this number * it's previous value
        /// </summary>
        private const double decay_weight = 0.9;

        /// <summary>
        /// Determines how harshly object weighting falls off as the strain decreases.
        /// </summary>
        private const double weighted_object_decay = 2.0;
        


        //no need to calculate old pp values; they're just the values gotten from server


        List<HitObject> HitObjects;
        double weightedObjectCount;


        int lastID = -1;


        public void CalculateValue(Score s, Beatmap m, out double sr, out float oldValue, out double newValue, out double acc, out string modString)
        {
            oldValue = s.pp;
            sr = -1;

            if (m.ID != lastID)
            {
                lastID = m.ID;
                //load hitobjects
                HitObjects = m.LoadHitObjects();
            }
            else
            {
                foreach (HitObject h in HitObjects)
                {
                    h.Strain = 1;
                    h.sameTypeSince = 1;
                    h.previousLengths = null;
                    h.IsValid = true;
                }
            }

            //hitobjects should be loaded, now calculate difficulty
            if (HitObjects.Count > 0)
            {
                bool nf = (s.enabled_mods & Mods[0]) > 0;
                bool ez = (s.enabled_mods & Mods[1]) > 0;
                bool hd = (s.enabled_mods & Mods[3]) > 0;
                bool hr = (s.enabled_mods & Mods[4]) > 0;
                bool sd = (s.enabled_mods & Mods[5]) > 0;
                bool dt = (s.enabled_mods & Mods[6]) > 0;
                bool ht = (s.enabled_mods & Mods[8]) > 0;
                bool nc = (s.enabled_mods & Mods[9]) > 0;
                bool fl = (s.enabled_mods & Mods[10]) > 0;
                bool pf = (s.enabled_mods & Mods[14]) > 0;

                double timerate = ((dt || nc) ? 1.5 : (ht ? 0.75 : 1.0));
                
                CalculateStrains(timerate);

                //Hitobjects now have strain values; calculate map strain

                sr = CalculateDifficulty() * star_scaling_factor;
                //weighted object count is also stored, recalculated each time

                newValue = StrainValue(s, sr) + AccValue(s, sr, hitWindow300(m, timerate, ez, hr));

                double multiplier = 1.1;

                if (nf)
                    multiplier *= 0.9;

                if (ez)
                    multiplier *= 0.9;
                
                if (fl) // flashlight bonus scales with length
                    multiplier *= Math.Min((1.02 * Math.Pow(weightedObjectCount / 15000.0, 1.5) + 1.05), 1.125);

                if (hd)
                {
                    multiplier *= 1.06;
                }

                if (hr)
                    multiplier *= 1.02;

                multiplier *= Math.Pow(.985, s.countmiss); //somewhat exponential loss for misses


                newValue *= multiplier;
            }
            else
            {
                newValue = -1;
            }
            acc = Acc(s);
            modString = getModString(s);
        }


        private double StrainValue(Score s, double starRating)
        {
            double strainValue = Math.Pow(starRating, 1.8) * 6.5 + 1.0;

            //double lengthBonus = Math.Min(1.0 + (weightedObjectCount / 20000.0), 1.2); 
            //double missDecay = Math.Min(0.99, 0.95 + (0.04 * Math.Sqrt((lengthBonus - 1) / 0.13))); // Caps at about 4000 weighted objects, misses lose .99

            //strainValue *= Math.Min(1.0, (weightedObjectCount + 3000.0) / 4000.0); // Scale down somewhat on shorter maps
            //strainValue *= Math.Min(1.0, (-1 / ((weightedObjectCount / 200) + 1)) + 1.1667);
            //strainValue *= lengthBonus; // Scale up somewhat for long and consistent maps

            //weight based on map length
            strainValue *= (weightedObjectCount < 1000) ? (Math.Log10(weightedObjectCount) / 2 - 0.5) : (Math.Log10(weightedObjectCount) / 3);

            return strainValue;
        }

        private double AccValue(Score s, double starRating, double hitWindow300)
        {
	        if (hitWindow300 <= 0)
	        {
		        return 0;
	        }

            // Values are based on experimentation.
            double accValue = (300.0 / (hitWindow300 + 21.0)); // Value is based on hitwindow
	        accValue *= 3.5; // Multiplier for sake of appropriate value; Adjust as necessary to balance acc and strain value
	        accValue *= Math.Pow(Acc(s), 12); // Scale with accuracy
            accValue *= Math.Pow(starRating, 1.1); // Scale with difficulty, slightly exponentially

            accValue *= 1 + 0.1f * Math.Min(1.0, weightedObjectCount / 1500.0); //slight scaling with length

            //accValue *= Math.Log10(weightedObjectCount) / 3;

            //accValue *= Math.Min(((weightedObjectCount + 2000) / 3000), 1.0); // Scale down for short maps, this multiplier capped at 1.0x so it cannot increase


            //accValue *= Math.Min(1.0, (-1 / ((weightedObjectCount / 100) + 1)) + 1.0908);
            //accValue *= Math.Min(1.0 + (weightedObjectCount / 15000), 1.33); // Scale up for long and consistent maps, with a high cap


            return accValue;
        }



        private void CalculateStrains(double timerate)
        {
            HitObject current, previous;
            previous = HitObjects[0];

            for (int index = 1; index < HitObjects.Count - 1; index++)
            {
                current = HitObjects[index];
                current.CalculateStrain(previous, timerate);
                previous = current;
            }
        }

        private double CalculateDifficulty()
        {
            double difficulty = 0;

            if (HitObjects.Count > 0)
            {
                List<double> highestStrains = new List<double>();
                List<HitObject> sortedObjects = new List<HitObject>(HitObjects);

                sortedObjects.Sort((a, b) => b.Strain.CompareTo(a.Strain));

                double maxStrain = sortedObjects[0].Strain;
                weightedObjectCount = 0;

                foreach (HitObject h in sortedObjects)
                {
                    if (h.IsValid)
                    {
                        h.IsValid = false;
                        highestStrains.Add(h.Strain);
                        h.InvalidateNear(strain_step);
                    }
                    double objectWeight = Math.Pow(h.Strain / maxStrain, weighted_object_decay);
                    weightedObjectCount += Math.Min(1, objectWeight);
                }
                
                double weight = 1;

                highestStrains.Sort((a, b) => b.CompareTo(a)); // Sort from highest to lowest strain.

                foreach (double strain in highestStrains)
                {
                    difficulty += weight * strain;
                    weight *= decay_weight;
                }
            }
            
            return difficulty;
        }

        private double Acc(Score s)
        {
            if (TotalHits(s) == 0)
                return 0;

            return Math.Max(0.0, Math.Min(1.0, (s.count100 + s.count300 * 2) / (TotalHits(s) * 2.0)));
        }
        private double TotalHits(Score s) => s.count300 + s.count100 + s.count50 + s.countmiss;

        private double hitWindow300(Beatmap m, double timerate, bool ez, bool hr)
        {
            double actualOD = m.OD;
            if (ez)
                actualOD /= 2.0;

            if (hr)
                actualOD *= 1.4;

            if (actualOD > 10)
                actualOD = 10;

            double hitwindow = Math.Floor(49 - (actualOD * 3)) + 0.5;

            if (Math.Abs(timerate - 1) > 0.1) //it's dt or ht
            {
                hitwindow /= timerate;
                hitwindow += (0.5 / timerate);
            }

            return hitwindow;
        }

        private string getModString(Score s)
        {
            int mods = s.enabled_mods;
            string value = "";

            for (int modID = 0; modID < Mods.Length; modID++)
            {
                if (modID != 5 && modID != 6)
                {
                    if ((mods & Mods[modID]) > 0)
                    {
                        value += ModStrings[modID];
                    }
                }
            }

            if ((mods & Mods[9]) == 0 && (mods & Mods[6]) > 0)
            {
                value += ModStrings[6];
            }
            if ((mods & Mods[5]) == 0 && (mods & Mods[14]) > 0)
            {
                value += ModStrings[14];
            }

            return value;
        }
    }
}
