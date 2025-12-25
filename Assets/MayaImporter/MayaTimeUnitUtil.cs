using System;
using System.Globalization;

namespace MayaImporter.Core
{
    /// <summary>
    /// Maya timeUnit -> fps / seconds scale
    /// Maya API無しで currentUnit -t を解釈するための最小ユーティリティ。
    /// </summary>
    public static class MayaTimeUnitUtil
    {
        /// <summary>
        /// Returns fps for frame-based units. Unknown -> defaultFps.
        /// </summary>
        public static double ResolveFramesPerSecond(string timeUnit, double defaultFps)
        {
            if (string.IsNullOrEmpty(timeUnit))
                return defaultFps;

            // Maya well-known names
            // (代表的なものだけ。未知は defaultFps へフォールバック)
            switch (timeUnit)
            {
                case "game": return 15.0;
                case "film": return 24.0;
                case "pal": return 25.0;
                case "ntsc": return 30.0;
                case "show": return 48.0;
                case "palf": return 50.0;
                case "ntscf": return 60.0;

                // Maya allows "23.976fps" like strings in some exports
                default:
                    break;
            }

            // pattern: "{number}fps"
            // ex: "23.976fps", "29.97fps", "59.94fps"
            if (timeUnit.EndsWith("fps", StringComparison.OrdinalIgnoreCase))
            {
                var n = timeUnit.Substring(0, timeUnit.Length - 3);
                if (double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps) && fps > 0.0)
                    return fps;
            }

            // time-based units: no fps semantics; we still return defaultFps (used only for frame conversion)
            return defaultFps;
        }

        /// <summary>
        /// True if the unit behaves like "frames" (film/ntsc/...) rather than seconds/minutes.
        /// </summary>
        public static bool IsFrameBased(string timeUnit)
        {
            if (string.IsNullOrEmpty(timeUnit)) return true;

            switch (timeUnit)
            {
                case "game":
                case "film":
                case "pal":
                case "ntsc":
                case "show":
                case "palf":
                case "ntscf":
                    return true;
            }

            if (timeUnit.EndsWith("fps", StringComparison.OrdinalIgnoreCase))
                return true;

            // time-based
            switch (timeUnit)
            {
                case "sec":
                case "min":
                case "hour":
                case "millisec":
                    return false;
            }

            // unknown -> frame-based 扱い（多くのMayaシーンがこれ）
            return true;
        }
    }
}
