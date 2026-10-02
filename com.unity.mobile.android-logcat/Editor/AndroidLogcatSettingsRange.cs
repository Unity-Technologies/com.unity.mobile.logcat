using System;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// The default and the accepted bounds of a numeric setting, kept in one place so
    /// that the slider the user drags, the clamping on the way in and the value a reset
    /// restores cannot drift apart.
    /// <para>
    /// A readonly struct rather than a record: a positional record needs
    /// <c>System.Runtime.CompilerServices.IsExternalInit</c>, which Unity's profile does
    /// not carry, so it would take a shim type of its own to compile. Nothing here needs
    /// the value equality a record would bring.
    /// </para>
    /// </summary>
    internal readonly struct SettingsRange
    {
        internal int Default { get; }
        internal int Min { get; }
        internal int Max { get; }

        internal SettingsRange(int defaultValue, int min, int max)
        {
            if (min > max)
                throw new ArgumentException($"Min {min} is greater than max {max}");
            if (defaultValue < min || defaultValue > max)
                throw new ArgumentException($"Default {defaultValue} is outside {min}..{max}");

            Default = defaultValue;
            Min = min;
            Max = max;
        }

        /// <summary>The value brought inside the bounds, for a value the user chose.</summary>
        internal int Clamp(int value)
        {
            return Math.Clamp(value, Min, Max);
        }

        /// <summary>
        /// The value if it is within bounds, otherwise <see cref="Default"/> - for a
        /// setting read back from a blob written before it existed, where it arrives as
        /// 0. Clamping such a value would quietly pick <see cref="Min"/>, which is not
        /// what a setting nobody has ever chosen should end up as.
        /// </summary>
        internal int OrDefault(int value)
        {
            return value < Min || value > Max ? Default : value;
        }
    }
}
