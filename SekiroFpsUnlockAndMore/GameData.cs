using System;
using System.Linq;
using System.Collections.Generic;

namespace SekiroFpsUnlockAndMore
{
    internal class GameData
    {
        internal const string PROCESS_NAME = "sekiro";
        internal const string PROCESS_TITLE = "Sekiro";
        internal const string PROCESS_DESCRIPTION = "Shadows Die Twice";
        internal const string PROCESS_EXE_VERSION = "1.2.0.0";


        /**
            <float>fFrameTick determines default frame rate limit in seconds
            000000014116168D | C743 18 8988883C          | mov dword ptr ds:[rbx+18],3C888889              | fFrameTick
            0000000141161694 | 4C:89AB 70020000          | mov qword ptr ds:[rbx+270],r13                  |
         */
        internal const string PATTERN_FRAMELOCK = "88 88 3C 4C 89 AB"; // first byte (last in mem) can can be 88/90 instead of 89 due to precision loss on floating point numbers
        internal const int PATTERN_FRAMELOCK_OFFSET = -1; // offset to byte array from found position
        internal const string PATTERN_FRAMELOCK_FUZZY = "C7 43 ?? ?? ?? ?? ?? 4C 89 AB";
        internal const int PATTERN_FRAMELOCK_FUZZY_OFFSET = 3;


        /**
            Reference pointer pFrametimeRunningSpeed to speed table entry that gets used in calculations. Add or remove multiplications of 4bytes to pFrametimeRunningSpeed address to use a higher or lower <float>fFrametimeCriticalRunningSpeed from table
            fFrametimeCriticalRunningSpeed should be roughly half the frame rate: 30 @ 60FPS limit, 50 @ 100FPS limit...
            00000001407D4E08 | F3:0F5905 90309202        | mulss xmm0,dword ptr ds:[1430F7EA0]             | pFrametimeRunningSpeed->fFrametimeCriticalRunningSpeed
         */
        internal const string PATTERN_FRAMELOCK_SPEED_FIX = "F3 0F 59 05 ?? 30 92 02";
        internal const int PATTERN_FRAMELOCK_SPEED_FIX_OFFSET = 4;
        /**
            00000001430F7E10
            Key: Patch to pFrametimeRunningSpeed last byte
            Value: Value resolve in float table from pFrametimeRunningSpeed->fFrametimeCriticalRunningSpeed
            Hardcoded cause lazy -> if anyone knows how the table is calculated then tell me and I'll buy you a beer
         */
        internal static readonly Dictionary<byte[], float> PATCH_FRAMELOCK_SPEED_FIX_MATRIX = new Dictionary<byte[], float>
        {
            { new byte[1] {0x68}, 15f },
            { new byte[1] {0x6C}, 16f },
            { new byte[1] {0x70}, 16.6667f },
            { new byte[1] {0x74}, 18f },
            { new byte[1] {0x78}, 18.6875f },
            { new byte[1] {0x7C}, 18.8516f },
            { new byte[1] {0x80}, 20f },
            { new byte[1] {0x84}, 24f },
            { new byte[1] {0x88}, 25f },
            { new byte[1] {0x8C}, 27.5f },
            { new byte[1] {0x90}, 30f },
            { new byte[1] {0x94}, 32f },
            { new byte[1] {0x98}, 38.5f },
            { new byte[1] {0x9C}, 40f },
            { new byte[1] {0xA0}, 48f },
            { new byte[1] {0xA4}, 49.5f },
            { new byte[1] {0xA8}, 50f },
            { new byte[1] {0xAC}, 57.2958f },
            { new byte[1] {0xB0}, 60f },
            { new byte[1] {0xB4}, 64f },
            { new byte[1] {0xB8}, 66.75f },
            { new byte[1] {0xBC}, 67f },
            { new byte[1] {0xC0}, 78.8438f },
            { new byte[1] {0xC4}, 80f },
            { new byte[1] {0xC8}, 84f },
            { new byte[1] {0xCC}, 90f },
            { new byte[1] {0xD0}, 93.8f },
            { new byte[1] {0xD4}, 100f },
            { new byte[1] {0xD8}, 120f },
            { new byte[1] {0xDC}, 127f },
            { new byte[1] {0xE0}, 128f },
            { new byte[1] {0xE4}, 130f },
            { new byte[1] {0xE8}, 140f },
            { new byte[1] {0xEC}, 144f },
            { new byte[1] {0xF0}, 150f }
        };
        internal static readonly byte[] PATCH_FRAMELOCK_SPEED_FIX_DISABLE = new byte[1] { 0x90 }; // 30f
        /// <summary>
        /// Finds closest valid speed fix value for a frame rate limit.
        /// </summary>
        /// <param name="frameLimit">The set frame rate limit.</param>
        /// <returns>The byte patch of the closest speed fix.</returns>
        internal static byte[] FindSpeedFixForRefreshRate(int frameLimit)
        {
            float idealSpeedFix = frameLimit / 2f;
            KeyValuePair<byte[], float> closestSpeedFix = new KeyValuePair<byte[], float>(PATCH_FRAMELOCK_SPEED_FIX_DISABLE, 30f);
            foreach (var speedFix in PATCH_FRAMELOCK_SPEED_FIX_MATRIX.OrderBy(kvp => kvp.Value))
            {
                if (Math.Abs(idealSpeedFix - speedFix.Value) < Math.Abs(idealSpeedFix - closestSpeedFix.Value))
                    closestSpeedFix = speedFix;
            }
            return closestSpeedFix.Key;
        }


        /**
            Reference pointer pCurrentResolutionWidth to <int>iInternalGameWidth (and <int>iInternalGameHeight which is +4 bytes)
            000000014114AC85 | 0F57D2                    | xorps xmm2,xmm2                                 |
            000000014114AC88 | 890D 92147D02             | mov dword ptr ds:[14391C120],ecx                | pCurrentResolutionWidth->iInternalGameWidth
            000000014114AC8E | 0F57C9                    | xorps xmm1,xmm1                                 |
            000000014114AC91 | 8915 8D147D02             | mov dword ptr ds:[14391C124],edx                | pCurrentResolutionHeight->iInternalGameHeight
         */
        internal const string PATTERN_RESOLUTION_POINTER = "0F 57 D2 89 0D ?? ?? ?? ?? 0F 57 C9";
        internal const int PATTERN_RESOLUTION_POINTER_OFFSET = 3;
        internal const int PATTERN_RESOLUTION_POINTER_INSTRUCTION_LENGTH = 6;


        /**
            DATA SECTION. All resolutions are listed in memory as <int>width1 <int>height1 <int>width2 <int>height2 ...
            Overwrite an unused one with desired new one. Some glitches, 1920x1080 and 1280x720 works best
         */
        internal const string PATTERN_RESOLUTION_DEFAULT = "80 07 00 00 38 04 00 00"; // 1920x1080
        internal const string PATTERN_RESOLUTION_DEFAULT_720 = "40 06 00 00 84 03 00 00"; // 1280x720
        internal static byte[] PATCH_RESOLUTION_DEFAULT_DISABLE = new byte[8] { 0x80, 0x07, 0x00, 0x00, 0x38, 0x04, 0x00, 0x00 };
        internal static byte[] PATCH_RESOLUTION_DEFAULT_DISABLE_720 = new byte[8] { 0x40, 0x06, 0x00, 0x00, 0x84, 0x03, 0x00, 0x00 };


        /**
            Conditional jump instruction that determines if 16/9 scaling for game is enforced or not, overwrite with non conditional JMP so widescreen won't get clinched
            000000014012967A | 74 47                     | je sekiro.1401296C3                             | conditional jump
            000000014012967C | 47:8B94C7 1C020000        | mov r10d,dword ptr ds:[r15+r8*8+21C]            | start of long resolution scaling calculation method within jump
         */
        internal const string PATTERN_RESOLUTION_SCALING_FIX = "47 47 8B 94 C7 1C 02 00 00";
        internal const int PATTERN_RESOLUTION_SCALING_FIX_OFFSET = -1;
        internal static byte[] PATCH_RESOLUTION_SCALING_FIX_ENABLE = new byte[1] { 0xEB };  // JMP
        internal static byte[] PATCH_RESOLUTION_SCALING_FIX_DISABLE = new byte[1] { 0x74 }; // JE


        /**
            Reference pointer pFovTableEntry to FOV entry in game FOV table that gets used in FOV calculations. Overwrite pFovTableEntry address to use a higher or lower <float>fFOV from table
            0000000140739548 | F3:0F1008                 | movss xmm1,dword ptr ds:[rax]                   |
            000000014073954C | F3:0F590D 0CE79B02        | mulss xmm1,dword ptr ds:[1430F7C60]             | pFovTableEntry->fFov
         */
        // credits to 'jackfuste' for original offset
        internal const string PATTERN_FOVSETTING = "F3 0F 10 08 F3 0F 59 0D ?? ?? 9B 02";
        internal const int PATTERN_FOVSETTING_OFFSET = 8;
        /**
            00000001430F7C60
            Key: Patch to pFovTableEntry (last 2 bytes)
            Value: Value resolve in float table from pFovTableEntry->fFov
         */
        internal static readonly Dictionary<byte[], string> PATCH_FOVSETTING_MATRIX = new Dictionary<byte[], string>
        {
            { new byte[2] {0x00, 0xE7}, "- 50%" },
            { new byte[2] {0x04, 0xE7}, "- 10%" },
            { new byte[2] {0x10, 0xE7}, "+ 15%" },
            { new byte[2] {0x42, 0x9B}, "+ 25%" },
            { new byte[2] {0x14, 0xE7}, "+ 40%" },
            { new byte[2] {0x18, 0xE7}, "+ 75%" },
            { new byte[2] {0x1C, 0xE7}, "+ 90%" }
        };
        internal static readonly byte[] PATCH_FOVSETTING_DISABLE = new byte[2] { 0x0C, 0xE7 }; // + 0%


        /**
            Reference pointer pPlayerStatsRelated to PlayerStats pointer, offset in struct to <int>iPlayerDeaths
            00000001407AAC92 | 0FB648 7A                 | movzx ecx,byte ptr ds:[rax+7A]                  |
            00000001407AAC96 | 888B F7000000             | mov byte ptr ds:[rbx+F7],cl                     |
            00000001407AAC9C | 48:8B05 4DD03903          | mov rax,qword ptr ds:[143B47CF0]                |
            00000001407AACA3 | 8B88 8C000000             | mov ecx,dword ptr ds:[rax+8C]                   |
            00000001407AACA9 | 898B F8000000             | mov dword ptr ds:[rbx+F8],ecx                   |
            00000001407AACAF | 48:8B05 3AD03903          | mov rax,qword ptr ds:[143B47CF0]                | pPlayerStatsRelated->[PlayerStats+0x90]->iPlayerDeaths
            00000001407AACB6 | 8B88 90000000             | mov ecx,dword ptr ds:[rax+90]                   | offset pPlayerStats->iPlayerDeaths
         */
        // credits to 'Me_TheCat' for original offset
        internal const string PATTERN_PLAYER_DEATHS = "0F B6 48 ?? 88 8B ?? ?? 00 00 48 8B 05 ?? ?? ?? ?? 8B 88 ?? ?? 00 00 89 8B ?? ?? 00 00 48 8B 05 ?? ?? ?? ?? 8B 88 ?? ?? 00 00";
        internal const int PATTERN_PLAYER_DEATHS_OFFSET = 29;
        internal const int PATTERN_PLAYER_DEATHS_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_PLAYER_DEATHS_POINTER_OFFSET_OFFSET = 9;


        /**
            Reference pointer pTotalKills to <int>iTotalKills, does not get updated on every kill but mostly on every 2nd, includes own player deaths...
            0000000141151838 | 48:8D0D A9A5B302          | lea rcx,qword ptr ds:[143C8BDE8]                | pTotalKills->iTotalKills
            000000014115183F | 891481                    | mov dword ptr ds:[rcx+rax*4],edx                |
            0000000141151842 | C3                        | ret                                             |
         */
        // credits to 'Me_TheCat' for original offset
        internal const string PATTERN_TOTAL_KILLS = "48 8D 0D ?? ?? ?? ?? 89 14 81 C3";
        internal const int PATTERN_TOTAL_KILLS_INSTRUCTION_LENGTH = 7;


        /**
            Reference pointer pTimeRelated to TimescaleManager pointer, offset in struct to <float>fTimescale which acts as a global speed scale for almost all ingame calculations
            0000000141149E87 | 48:8B05 3A24B402          | mov rax,qword ptr ds:[143C8C2C8]                | pTimeRelated->[TimescaleManager+0x360]->fTimescale
            0000000141149E8E | F3:0F1088 60030000        | movss xmm1,dword ptr ds:[rax+360]               | offset TimescaleManager->fTimescale
            0000000141149E96 | F3:0F5988 68020000        | mulss xmm1,dword ptr ds:[rax+268]               |
         */
        // credits to 'Zullie the Witch' for original offset
        internal const string PATTERN_TIMESCALE = "48 8B 05 ?? ?? ?? ?? F3 0F 10 88 ?? ?? ?? ?? F3 0F";
        internal const int PATTERN_TIMESCALE_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_TIMESCALE_POINTER_OFFSET_OFFSET = 11;


        /**
            Reference pointer pPlayerStructRelated1 to 4 more pointers up to player data class, offset in struct to <float>fTimescalePlayer which acts as a speed scale for the player character
            00000001406BF1D7 | 48:8B1D 128C4A03          | mov rbx,qword ptr ds:[143B67DF0]                | pPlayerStructRelated1->[pPlayerStructRelated2+0x88]->[pPlayerStructRelated3+0x1FF8]->[pPlayerStructRelated4+0x28]->[pPlayerStructRelated5+0xD00]->fTimescalePlayer
            00000001406BF1DE | 48:85DB                   | test rbx,rbx                                    |
            00000001406BF1E1 | 74 3C                     | je sekiro.1406BF21F                             |
            00000001406BF1E3 | 8B17                      | mov edx,dword ptr ds:[rdi]                      |
         */
        // credits to 'Zullie the Witch' for original offset
        internal const string PATTERN_TIMESCALE_PLAYER = "48 8B 1D ?? ?? ?? ?? 48 85 DB 74 3C 8B 17";
        internal const int PATTERN_TIMESCALE_PLAYER_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_TIMESCALE_POINTER2_OFFSET = 0x88;
        internal const int PATTERN_TIMESCALE_POINTER3_OFFSET = 0x1FF8;
        internal const int PATTERN_TIMESCALE_POINTER4_OFFSET = 0x28;
        internal const int PATTERN_TIMESCALE_POINTER5_OFFSET = 0xD00;


        /**
            Controls camera pitch. xmm4 holds new pitch from a calculation while rps+170 holds current one from mouse so we overwrite xmm4 with the old pitch value
            000000014073AF26 | 0F29A5 70080000            | movaps xmmword ptr ss:[rbp+870],xmm4           | code inject overwrite from here
            000000014073AF2D | 0F29A5 80080000            | movaps xmmword ptr ss:[rbp+880],xmm4           | jump back here from code inject
            000000014073AF34 | 0F29A6 70010000            | movaps xmmword ptr ds:[rsi+170],xmm4           | camPitch, newCamPitch
            000000014073AF3B | EB 1C                      | jmp sekiro.14073AF59                           |
            000000014073AF3D | F3:0F108E 74010000         | movss xmm1,dword ptr ds:[rsi+174]              | 
         */
        internal const string PATTERN_CAMADJUST_PITCH = "0F 29 ?? ?? ?? 00 00 0F 29 ?? ?? ?? 00 00 0F 29 ?? ?? ?? 00 00 EB ?? F3";
        internal const int INJECT_CAMADJUST_PITCH_OVERWRITE_LENGTH = 7;
        internal static readonly byte[] INJECT_CAMADJUST_PITCH_SHELLCODE = new byte[]
        {
            0x0F, 0x28, 0xA6, 0x70, 0x01, 0x00, 0x00,   // movaps xmm4,xmmword ptr ds:[rsi+170]
            0x0F, 0x29, 0xA5, 0x70, 0x08, 0x00, 0x00    // movaps xmmword ptr ss:[rbp+870],xmm4
        };
        /**
            Controls automatic camera yaw adjust on move on Z-axis. xmm0 holds new yaw while rsi+174 holds current one prior movement so we overwrite xmm0 with the old yaw value
            000000014073AF4C | E8 6F60FFFF                | call sekiro.140730FC0                          |
            000000014073AF51 | F3:0F1186 74010000         | movss dword ptr ds:[rsi+174],xmm0              | camYaw, newCamYaw | code inject overwrite from here
            000000014073AF59 | 80BE A3020000 00           | cmp byte ptr ds:[rsi+2A3],0                    | jump back here from code inject
            000000014073AF60 | 0F84 2F020000              | je sekiro.14073B195                            |
         */
        internal const string PATTERN_CAMADJUST_YAW_Z = "E8 ?? ?? ?? ?? F3 ?? ?? ?? ?? ?? 00 00 80 ?? ?? ?? 00 00 00 0F 84";
        internal const int PATTERN_CAMADJUST_YAW_Z_OFFSET = 5;
        internal const int INJECT_CAMADJUST_YAW_Z_OVERWRITE_LENGTH = 8;
        internal static readonly byte[] INJECT_CAMADJUST_YAW_Z_SHELLCODE = new byte[]
        {
            0xF3, 0x0F, 0x10, 0x86, 0x74, 0x01, 0x00, 0x00, // movss xmm0,dword ptr ds:[rsi+174]
            0xF3, 0x0F, 0x11, 0x86, 0x74, 0x01, 0x00, 0x00  // movss dword ptr ds:[rsi+174],xmm0
        };
        /**
            Controls automatic camera pitch adjust on move on XY-axis. Pointer in rax holds new pitch while rsi+170 holds current one prior movement so we overwrite xmm0 with the old pitch value and then overwrite [rax] with xmm0
            000000014073B476 | F3:0F1000                  | movss xmm0,dword ptr ds:[rax]                  | newCamPitch | code inject overwrite from here
            000000014073B47A | F3:0F1186 70010000         | movss dword ptr ds:[rsi+170],xmm0              | camPitch
            000000014073B482 | F3:0F1085 E4120000         | movss xmm0,dword ptr ss:[rbp+12E4]             | jump back here from code inject
            000000014073B48A | E8 91BDFFFF                | call sekiro.140737220                          |
            000000014073B48F | 0F28D0                     | movaps xmm2,xmm0                               |
         */
        // thanks to 'Cielos' for original offset
        internal const string PATTERN_CAMADJUST_PITCH_XY = "F3 ?? ?? ?? F3 ?? ?? ?? ?? ?? 00 00 F3 ?? ?? ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F";
        internal const int INJECT_CAMADJUST_PITCH_XY_OVERWRITE_LENGTH = 12;
        internal static readonly byte[] INJECT_CAMADJUST_PITCH_XY_SHELLCODE = new byte[]
        {
            0xF3, 0x0F, 0x10, 0x86, 0x70, 0x01, 0x00, 0x00, // movss xmm0,dword ptr ds:[rsi+170]
            0xF3, 0x0F, 0x11, 0x00,                         // movss dword ptr ds:[rax],xmm0
            0xF3, 0x0F, 0x10, 0x00,                         // movss xmm0,dword ptr ds:[rax]
            0xF3, 0x0F, 0x11, 0x86, 0x70, 0x01, 0x00, 0x00  // movss dword ptr ds:[rsi+170],xmm0
        };
        /**
            Controls automatic camera yaw adjust on move on XY-axis. xmm0 new yaw while rsi+174 holds current one prior movement so we overwrite xmm0 with the old yaw value
            000000014073B564 | E8 B7BCFFFF                | call sekiro.140737220                          | 
            000000014073B569 | F3:0F1186 74010000         | movss dword ptr ds:[rsi+174],xmm0              | camYaw, newCamYaw | code inject overwrite from here
            000000014073B571 | E9 9A020000                | jmp sekiro.14073B810                           | jump back here from code inject
         */
        // thanks to 'Cielos' for original offset
        internal const string PATTERN_CAMADJUST_YAW_XY = "E8 ?? ?? ?? ?? F3 0F 11 86 74 01 00 00 E9";
        internal const int PATTERN_CAMADJUST_YAW_XY_OFFSET = 5;
        internal const int INJECT_CAMADJUST_YAW_XY_OVERWRITE_LENGTH = 8;
        internal static readonly byte[] INJECT_CAMADJUST_YAW_XY_SHELLCODE = new byte[]
        {
            0xF3, 0x0F, 0x10, 0x86, 0x74, 0x01, 0x00, 0x00, // movss xmm0,dword ptr ds:[rsi+174]
            0xF3, 0x0F, 0x11, 0x86, 0x74, 0x01, 0x00, 0x00  // movss dword ptr ds:[rsi+174],xmm0
        };
    }
}
