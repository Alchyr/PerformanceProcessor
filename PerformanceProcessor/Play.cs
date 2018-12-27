using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceProcessor
{
    class Play
    {
        public string user { get; set; }
        public string map { get; set; }

        public double acc { get; set; }
        public int maxCombo { get; set; }
        public string mods { get; set; }

        public float oldVal { get; set; }
        public double newVal { get; set; }

        public double difficulty { get; set; }

        public Play(string user, string map, string mods, double difficulty, float oldPP, double newPP, double accuracy, int combo)
        {
            this.user = user;
            this.map = map;
            this.mods = mods;
            this.difficulty = difficulty;
            this.oldVal = oldPP;
            this.newVal = newPP;
            this.acc = accuracy;
            this.maxCombo = combo;
        }


        public string ToString(bool user)
        {
            string formatted = "";

            if (user)
            {
                formatted += this.user + "; ";
            }
            formatted += map + "; " + Math.Round(difficulty, 2).ToString() + "* ";
            formatted += Math.Round(acc * 100, 2).ToString() + "% " + maxCombo + "x " + mods + "; ";
            formatted += "Old PP: " + Math.Round(oldVal, 2).ToString() + ", New PP: " + Math.Round(newVal, 2).ToString();

            return formatted;
        }
    }
}
