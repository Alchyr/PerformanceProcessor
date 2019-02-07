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

        private const double star_scaling_factor = 0.08;

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

                //newValue = StrainValue(s, sr) + AccValue(s, sr, hitWindow300(m, timerate, ez, hr));
                //newValue = PlayValue(s, sr, hitWindow300(m, timerate, ez, hr));

                double multiplier = 1.1;

                if (nf)
                    multiplier *= 0.9;

                if (ez)
                    multiplier *= 0.9;
                
                if (fl) // flashlight bonus scales with length
                    multiplier *= Math.Min((1.02 * Math.Pow(weightedObjectCount / 15000.0, 1.5) + 1.05), 1.125);

                if (hd)
                {
                    if (ez)
                    {
                        multiplier *= 1.05;
                    }
                    else
                    {
                        multiplier *= 1.1;
                    }
                }

                if (hr)
                    multiplier *= 1.05;


                //Using old calculation

                //double strainValue = oldComputeStrainValue(s, sr);
                //double accuracyValue = oldComputeAccValue(s, hitWindow300(m, timerate, ez, hr));

                //Using new calculation

                double strainValue = StrainValue(s, sr);
                double accuracyValue = AccValue(s, sr, hitWindow300(m, timerate, ez, hr));

                double totalValue =
                    Math.Pow(
                        Math.Pow(strainValue, 1.2) +
                        Math.Pow(accuracyValue, 1.2), 1.0 / 1.2
                    );



                //multiplier *= Math.Pow(.9825, s.countmiss); //somewhat exponential loss for misses

                //weight based on map length
                //multiplier *= (weightedObjectCount < 1000) ? (Math.Log10(weightedObjectCount / 10) / 2) : (Math.Log10(weightedObjectCount) / 3);
                //multiplier *= (Math.Log10(weightedObjectCount) / 3.1);

                newValue = totalValue;
                newValue *= multiplier;
            }
            else
            {
                newValue = -1;
            }
            acc = Acc(s);
            modString = getModString(s);
        }


        private double PlayValue(Score s, double starRating, double hitWindow300)
        {
            if (hitWindow300 <= 0 || starRating <= 0)
            {
                return 0;
            }

            // Values are based on experimentation.
            double value = Math.Pow(starRating, 1.95) * Math.Pow(Acc(s), 4.5) * 15; // Value is based on star rating and accuracy

            value -= 5 * Math.Log10(hitWindow300 - 10) * Math.Pow(starRating, 1.95) * Math.Pow(Acc(s), 7); // Scale based on hitwindow. Larger hitwindow, more loss.

            return value;
        }


        private double StrainValue(Score s, double starRating)
        {
            double strainValue = Math.Pow(starRating, 1.85) * 6.5 + 0.1; //0.1 gives a small minimum

            strainValue *= Math.Min(1.25, Math.Pow(weightedObjectCount / 1000.0, 0.2));

            strainValue *= Math.Pow(Acc(s), 2);

            return strainValue;
        }
        private double AccValue(Score s, double starRating, double hitWindow300)
        {
	        if (hitWindow300 <= 0)
	        {
		        return 0;
	        }

            // Values are based on experimentation.
            double accValue = (300.0 / (hitWindow300 + 21.0)) * 2; // Value is based on hitwindow
	        accValue *= Math.Pow(Acc(s), 14); // Scale with accuracy
            accValue *= Math.Pow(starRating, 1.5); // Scale with difficulty, slightly exponentially

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer
            return accValue * Math.Min(1.15, Math.Pow(weightedObjectCount / 1000.0, 0.2));

            //accValue *= 1 + 0.1f * Math.Min(1.0, weightedObjectCount / 1500.0); //slight scaling with length

            //accValue *= Math.Log10(weightedObjectCount) / 3;

            //accValue *= Math.Min(((weightedObjectCount + 2000) / 3000), 1.0); // Scale down for short maps, this multiplier capped at 1.0x so it cannot increase


            //accValue *= Math.Min(1.0, (-1 / ((weightedObjectCount / 100) + 1)) + 1.0908);
            //accValue *= Math.Min(1.0 + (weightedObjectCount / 15000), 1.33); // Scale up for long and consistent maps, with a high cap


            //return accValue;
        }



        //OLD CALCULATIONS ___________________________________________________________________

        private double oldComputeStrainValue(Score s, double starRating)
        {
            double strainValue = Math.Pow(5.0 * Math.Max(1.0, starRating / 0.0075) - 4.0, 2.0) / 100000.0;

            // Longer maps are worth more
            double lengthBonus = 1 + 0.1f * Math.Min(1.0, weightedObjectCount / 1000.0);
            strainValue *= lengthBonus;

            // Penalize misses exponentially. This mainly fixes tag4 maps and the likes until a per-hitobject solution is available
            strainValue *= Math.Pow(0.985, s.countmiss);

            // Scale the speed value with accuracy _slightly_
            return strainValue * Acc(s);
        }
        private double oldComputeAccValue(Score s, double hitWindow300)
        {
            if (hitWindow300 <= 0)
                return 0;

            // Lots of arbitrary values from testing.
            // Considering to use derivation from perfect accuracy in a probabilistic manner - assume normal distribution
            double accValue = Math.Pow(150.0 / hitWindow300, 1.1) * Math.Pow(Acc(s), 15) * 22.0;

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer
            return accValue * Math.Min(1.15, Math.Pow(weightedObjectCount / 1000.0, 0.25));
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
