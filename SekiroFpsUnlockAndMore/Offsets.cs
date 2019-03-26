
namespace SekiroFpsUnlockAndMore
{
    internal class Offsets
    {
        internal const string PATTERN_FRAMELOCK = "00 88 88 3C 4C 89 AB 00"; // ?? 88 88 3C 4C 89 AB ?? // pattern/signature of frame rate limiter, first byte (last in mem) can can be 88/90 instead of 89 due to precision loss on floating point numbers
        internal const string PATTERN_FRAMELOCK_MASK = "?xxxxxx?"; // mask for frame rate limiter signature scanning
        internal const string PATTERN_FRAMELOCK_LONG = "44 88 6B 00 C7 43 00 89 88 88 3C 4C 89 AB 00 00 00 00"; // 44 88 6B ?? C7 43 ?? 89 88 88 3C 4C 89 AB ?? ?? ?? ??
        internal const string PATTERN_FRAMELOCK_LONG_MASK = "xxx?xx?xxxxxxx????";
        internal const int PATTERN_FRAMELOCK_LONG_OFFSET = 7;
        internal const string PATTERN_FRAMELOCK_FUZZY = "C7 43 00 00 00 00 00 4C 89 AB 00 00 00 00";  // C7 43 ?? ?? ?? ?? ?? 4C 89 AB ?? ?? ?? ??
        internal const string PATTERN_FRAMELOCK_FUZZY_MASK = "xx?????xxx????";
        internal const int PATTERN_FRAMELOCK_FUZZY_OFFSET = 3; // offset to byte array from found position
        internal const string PATTERN_FRAMELOCK_RUNNING_FIX = "F3 0F 59 05 00 30 92 02 0F 2F F8"; // F3 0F 59 05 ?? 30 92 02 0F 2F F8 | 0F 51 C2 F3 0F 59 05 ?? ?? ?? ?? 0F 2F F8
        internal const string PATTERN_FRAMELOCK_RUNNING_FIX_MASK = "xxxx?xxxxxx";
        internal const int PATTERN_FRAMELOCK_RUNNING_FIX_OFFSET = 4;
        internal const string PATTERN_RESOLUTION_POINTER = "0F 57 D2 89 0D 00 00 00 00 0F 57 C9 89 15 00 00 00 00"; // 0F 57 D2 89 0D ?? ?? ?? ?? 0F 57 C9 89 15 ?? ?? ?? ??
        internal const string PATTERN_RESOLUTION_POINTER_MASK = "xxxxx????xxxxx????";
        internal const int PATTERN_RESOLUTION_POINTER_OFFSET = 3;
        internal const int PATTERN_RESOLUTION_POINTER_INSTRUCTION_LENGTH = 6;
        internal const string PATTERN_RESOLUTION_DEFAULT = "80 07 00 00 38 04"; // 1920x1080
        internal const string PATTERN_RESOLUTION_DEFAULT_MASK = "xxxxxx";
        internal const string PATTERN_WIDESCREEN_219 = "00 47 47 8B 94 C7 1C 02 00 00"; // ?? 47 47 8B 94 C7 1C 02 00 00
        internal const string PATTERN_WIDESCREEN_219_MASK = "?xxxxxxxxx";

        // credits to jackfuste for FOV findings
        internal const string PATTERN_FOVSETTING = "F3 0F 10 08 F3 0F 59 0D 00 E7 9B 02"; // F3 0F 10 08 F3 0F 59 0D ?? E7 9B 02
        internal const string PATTERN_FOVSETTING_MASK = "xxxxxxxx?xxx";
        internal const int PATTERN_FOVSETTING_OFFSET = 8;
    }
}
