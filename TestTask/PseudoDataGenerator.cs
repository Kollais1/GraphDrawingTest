namespace TestTask
{
    static class PseudoDataGenerator
    {
        public static float MagnitudeMaxValue = -20;
        public static float MagnitudeMinValue = -120;
        public static float FrequencyMaxValueAbs = 110;
        public static float FrequencyMinValueAbs = 90;
        public static float FrequencyMaxValue = 110;
        public static float FrequencyMinValue = 90;
        public static float FrequencyRange = Math.Abs(FrequencyMaxValue - FrequencyMinValue);
        public static float FrequencyRangeAbs = Math.Abs(FrequencyMaxValueAbs - FrequencyMinValueAbs);
        public static float MagnitudeRange = Math.Abs(MagnitudeMaxValue - MagnitudeMinValue);
        private static Random _random = new();
        private static float _noiseLevelPercent = 0.1F;

        public static PseudoData Generate()
        {
            return new PseudoData(
                GetRandomNumberInRange(_random, MagnitudeMinValue, MagnitudeMaxValue),
                GetRandomNumberInRange(_random, FrequencyMinValue, FrequencyMaxValue));
        }

        public static PseudoData Generate(int i, int max)
        {
            return new PseudoData(
                SimulateSignal(i, max),
                FrequencyMinValueAbs + i * FrequencyRangeAbs / max);
        }

        public static float GetRandomNumberInRange(Random random, float minNumber, float maxNumber)
        {
            return ((float)random.NextDouble()) * (maxNumber - minNumber) + minNumber;
        }

        private static float SimulateSignal(int i, int max)
        {
            //define parts
            //first flat part: from 0 to 0.4
            //first raised flat part: 0.4 to 0.45
            //peak part: 0.45 to 0.55
            //second raised flat part: 0.55 to 0.6
            //second flat part: from 0.6 to 1
            //flat part - minimum magnitude
            //raised flat part - 0.25 of max magnitude
            //peak from 0.7 max magnitude
            //then add random noise

            float index = (float) i / max;
            if (index < 0.399 || index > 0.6)
                return MagnitudeMinValue + GetRandomNumberInRange(_random, 0, MagnitudeRange) * _noiseLevelPercent;

            else if (index < 0.45 || index > 0.55)
                return MagnitudeMinValue * 0.75F + GetRandomNumberInRange(_random, 0, MagnitudeRange) * _noiseLevelPercent;

            else
                return MagnitudeMinValue * 0.3F + GetRandomNumberInRange(_random, 0, MagnitudeRange) * _noiseLevelPercent;
        }
    }

    class PseudoData : IComparable
    {
        public float Magnitude;
        public float Frequency;
        public PseudoData(float magnitude, float frequency)
        {
            Magnitude = magnitude;
            Frequency = frequency;
        }

        public int CompareTo(object? obj)
        {
            if (obj == null) return 1;

            if (obj is PseudoData otherData)
                return Frequency.CompareTo(otherData.Frequency);
            else
                throw new ArgumentException("Object is not comparable");
        }
    }
}
