using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;

namespace PerformanceProcessor
{
    public class HitObject
    {
        static Regex sliderExpression = new Regex("^([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+),[BLP](?:\\|([\\-0-9]+:[\\-0-9]+))+,([0-9]+),([0-9\\.]+)");
        static Regex spinnerExpression = new Regex("^([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+)(?:,[0-9]:[0-9]:[0-9]:[0-9]:)?$");
        static Regex circleExpression = new Regex("^([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+)(?:,[0-9]:[0-9]:[0-9]:[0-9]:)?$");

        /// <summary>
        /// Factor by how much individual / overall strain decays per second.
        /// </summary>
        internal const double DECAY_BASE = 0.30;

        private const double base_speed_value = 0.925; //the default addition value
        private const double color_speed_value = 1.2; //the addition value for don/kat speed
        private const double color_decay_scale = 250;

        //type
        private const double type_change_scale = 0.7; // Scales the final addition value.

        private const double base_type_bonus = 1.5; // The maximum bonus receivable when type changes
        private const double type_bonus_scale = 1.75; // Determines how bonus is scaled with number of objects of same type
        private const double type_swap_adjust = 0.65; //Addition to denominator of bonus - affects how it scales
        private const double type_bonus_cap = 1.25; // Determines maximum bonus when swapping

        private const double same_typeswitch_loss = 0.8; // The loss in bonus from going from repeating even -> even or odd -> odd
        private const double even_typeswitch_loss = 0.7; // The loss in bonus from going from repeating even -> even or odd -> odd
        private const double close_repeat_loss = 0.65; // The loss in bonus from repeating the same length of object twice in a row (per color)
        private const double late_repeat_loss = 0.8; // The loss in bonus from repeating the same length of object with a gap between (per color)

        //rhythm
        private const double tiny_speedup_bonus = 0.25; // Very small speed increases
        private const double small_speedup_bonus = 1.0; // This mostly affects 1/4 -> 1/6 and other weird rhythms.
        private const double moderate_speedup_bonus = 0.6; // Speed doubling
        private const double large_speedup_bonus = 0.4; // Anything that more than doubles speed. Affects doubles.

        private const double tiny_speeddown_bonus = 0.15; // Very small speed decrease
        private const double small_speeddown_bonus = 0.4; // This mostly affects 1/6 -> 1/4, and other weird rhythms.
        private const double large_speeddown_bonus = 0.2; // Half speed; for slowdown, no need for more specific.

        public enum ObjectType
        {
            Circle = 1,
            Slider = 2,
            Spinner = 8
        }




        // Variables
        public int pos { get; set; }
        public int hitsounds { get; set; }
        public bool kat { get; set; } = false;

        public double Strain = 0.0;

        public double BaseStrain = 0.0;
        public double DonStrain = 0.0;
        public int lastDonPos = 0;
        public double KatStrain = 0.0;
        public int lastKatPos = 0;

        public ObjectType type { get; set; }

        
        private double timeElapsed;
        public int sameTypeSince = 1;

        public int[][] previousLengths = null;
        

        public HitObject previousHitObject;
        public HitObject nextHitObject;
        public bool IsValid = true;


        internal void CalculateStrain(HitObject previousHitObject, double timeRate)
        {
            // Form a linkedlist for ease of invalidating objects during calculation
            previousHitObject.nextHitObject = this;
            this.previousHitObject = previousHitObject;

            timeElapsed = (pos - previousHitObject.pos) / timeRate;


            //Decay
            double decay = Math.Max(0, Math.Pow(DECAY_BASE, timeElapsed / 1000) - 0.01);
            if (timeElapsed > 1000) // Objects more than 1 second apart gain somewhat exponentially less strain.
            {
                decay /= 1 + ((timeElapsed - 1000) / 100);
            }

            //Scaling as speed increases
            double rhythmAdditionScale = 0.5 + (0.5 * timeElapsed / 65.0);
            if (timeElapsed > 65)
            {
                rhythmAdditionScale = 1;
            }

            double typeAddition = 0;
            double rhythmAddition = 0;


            // Only if not a slider or spinner is any rhythm/type strain added
            if (previousHitObject.type == ObjectType.Circle && type == ObjectType.Circle) // And we only want to check out hitobjects which aren't so far in the past
            {
                // To remove value of sliders/spinners, set default addition to 0 along with type and rhythm additions, and increase to 1 here
                typeAddition = typeChangeAddition(previousHitObject) * decay * type_change_scale;
                rhythmAddition = rhythmChangeAddition(previousHitObject) * decay * rhythmAdditionScale;
            }


            // Speed
            BaseStrain = (previousHitObject.BaseStrain + base_speed_value + typeAddition + rhythmAddition) * decay;

            if (kat)
            {
                DonStrain = previousHitObject.DonStrain;
                lastDonPos = previousHitObject.lastDonPos;

                double katElapsed = (pos - previousHitObject.lastKatPos) / timeRate;
                decay = Math.Max(0, Math.Pow(DECAY_BASE, katElapsed / color_decay_scale) - 0.01);

                KatStrain = (previousHitObject.KatStrain + color_speed_value) * decay;

                lastKatPos = pos;

                Strain = (BaseStrain + (KatStrain * 0.5)) / 1.5;
            }
            else
            {
                KatStrain = previousHitObject.KatStrain;
                lastKatPos = previousHitObject.lastKatPos;

                double donElapsed = (pos - previousHitObject.lastDonPos) / timeRate;
                decay = Math.Max(0, Math.Pow(DECAY_BASE, donElapsed / color_decay_scale) - 0.01);

                DonStrain = (previousHitObject.DonStrain + color_speed_value) * decay;

                lastDonPos = pos;

                Strain = (BaseStrain + (DonStrain * 0.5)) / 1.5;
            }
        }

        private double typeChangeAddition(HitObject previousHitObject)
        {
            previousLengths = previousHitObject.previousLengths;

            // This occurs when the previous object is a slider or spinner, or on the first object. Since key doesn't matter for those, count being reset is fine.
            if (previousLengths == null)
            {
                previousLengths = new int[][] { new int[] { 0, 0 }, new int[] { 0, 0 } };
            }

            // If we don't have the same hit type, trigger a type change!
            if (previousHitObject.kat ^ kat) // for bool xor is equivalent to != so either could be used
            {
                double typeBonus = base_type_bonus - (type_bonus_scale / (previousHitObject.sameTypeSince + type_swap_adjust));
                double multiplier = 1.0;


                if (previousHitObject.kat) // Previous is kat
                {
                    if (previousHitObject.sameTypeSince % 2 == previousLengths[0][0] % 2) //previous don length was same even/odd
                        multiplier *= same_typeswitch_loss;

                    if (previousHitObject.sameTypeSince % 2 == 0)
                        multiplier *= even_typeswitch_loss;

                    if (previousLengths[1][0] == previousHitObject.sameTypeSince)
                        multiplier *= close_repeat_loss;

                    if (previousLengths[1][1] == previousHitObject.sameTypeSince)
                        multiplier *= late_repeat_loss;

                    previousLengths[1][1] = previousLengths[1][0];
                    previousLengths[1][0] = previousHitObject.sameTypeSince;
                }
                else // Don
                {
                    if (previousHitObject.sameTypeSince % 2 == previousLengths[1][0] % 2) //previous kat length was same even/odd
                        multiplier *= same_typeswitch_loss;

                    if (previousHitObject.sameTypeSince % 2 == 0)
                        multiplier *= even_typeswitch_loss;

                    if (previousLengths[0][0] == previousHitObject.sameTypeSince)
                        multiplier *= close_repeat_loss;

                    if (previousLengths[0][1] == previousHitObject.sameTypeSince)
                        multiplier *= late_repeat_loss;

                    previousLengths[0][1] = previousLengths[0][0];
                    previousLengths[0][0] = previousHitObject.sameTypeSince;
                }

                return Math.Min(type_bonus_cap, typeBonus * multiplier);
            }
            // No type change? Increment counter
            else
            {
                sameTypeSince = previousHitObject.sameTypeSince + 1;
                return 0;
            }
        }

        private double rhythmChangeAddition(HitObject previousHitObject)
        {
            // We don't want a division by zero if some random mapper decides to put 2 HitObjects at the same time.
            if (previousHitObject.timeElapsed == 0)
                return 0;

            double change = (timeElapsed / previousHitObject.timeElapsed);

            if (change < 0.48) // Speedup by more than 2x
                return large_speedup_bonus;
            else if (change <= .51) // Speedup by 2x
                return moderate_speedup_bonus;
            else if (change <= 0.9) // Speedup between small amount and 2x
                return small_speedup_bonus;
            else if (change < .95) // Speedup a very small amount
                return tiny_speedup_bonus;
            else if (change > 1.95) // Slowdown by half speed or more
                return large_speeddown_bonus;
            else if (change > 1.15) //Slowdown less than half speed
                return small_speeddown_bonus;
            else if (change > 1.02) //Slowdown a very small amount
                return tiny_speeddown_bonus;

            return 0;
        }

        internal void InvalidateNear(double actualStrainStep)
        {
            InvalidatePrevious(actualStrainStep);
            InvalidateNext(actualStrainStep);
        }
        private void InvalidatePrevious(double actualStrainStep)
        {
            if (previousHitObject != null)
            {
                if (pos - previousHitObject.pos < actualStrainStep)
                {
                    previousHitObject.IsValid = false;
                    previousHitObject.InvalidatePrevious(actualStrainStep - (pos - previousHitObject.pos));
                }
            }
        }
        private void InvalidateNext(double actualStrainStep)
        {
            if (nextHitObject != null)
            {
                if (nextHitObject.pos - pos < actualStrainStep)
                {
                    nextHitObject.IsValid = false;
                    nextHitObject.InvalidateNext(actualStrainStep - (nextHitObject.pos - pos));
                }
            }
        }

        public static HitObject FromString(string input)
        {
            if (input.Length == 0)
                return null;

            HitObject hitobject = null;

            if (sliderExpression.IsMatch(input))
            {
                hitobject = new HitObject();
                hitobject.type = ObjectType.Slider;

                Match result = sliderExpression.Match(input);
                
                hitobject.pos = int.Parse(result.Groups[3].Value);
            }
            else if (spinnerExpression.IsMatch(input))
            {
                hitobject = new HitObject();
                hitobject.type = ObjectType.Spinner;

                Match result = spinnerExpression.Match(input);
                
                hitobject.pos = int.Parse(result.Groups[3].Value);
            }
            else if (circleExpression.IsMatch(input))
            {
                hitobject = new HitObject();
                hitobject.type = ObjectType.Circle;

                Match result = circleExpression.Match(input);
                
                hitobject.pos = int.Parse(result.Groups[3].Value);

                hitobject.hitsounds = int.Parse(result.Groups[5].Value);

                if ((hitobject.hitsounds & 8) > 0 || (hitobject.hitsounds & 2) > 0) //if clap or whistle, this is a kat
                {
                    hitobject.kat = true;
                }
            }
            return hitobject;
        }
    }
}
