using System;

namespace SekiroFpsUnlockAndMore
{
    internal class GameData
    {
        internal const string PROCESS_NAME = "sekiro";
        internal const string PROCESS_TITLE = "Sekiro";
        internal const string PROCESS_DESCRIPTION = "Shadows Die Twice";
        internal const string PROCESS_EXE_VERSION = "1.2.0.0";


        /**
            <float>fFrameTick determines default frame rate limit in seconds.
            000000014116168D | C743 18 8988883C          | mov dword ptr ds:[rbx+18],3C888889              | fFrameTick
            0000000141161694 | 4C:89AB 70020000          | mov qword ptr ds:[rbx+270],r13                  |
         */
        internal const string PATTERN_FRAMELOCK = "88 88 3C 4C 89 AB"; // first byte can can be 88/90 instead of 89 due to precision loss on floating point numbers
        internal const int PATTERN_FRAMELOCK_OFFSET = -1; // offset to byte array from found position
        internal const string PATTERN_FRAMELOCK_FUZZY = "C7 43 ?? ?? ?? ?? ?? 4C 89 AB";
        internal const int PATTERN_FRAMELOCK_FUZZY_OFFSET = 3;


        /**
            Reference pointer pFrametimeRunningSpeed to speed table entry that gets used in calculations. 
            Add or remove multiplications of 4bytes to pFrametimeRunningSpeed address to use a higher or lower <float>fFrametimeCriticalRunningSpeed from table.
            fFrametimeCriticalRunningSpeed should be roughly half the frame rate: 30 @ 60FPS limit, 50 @ 100FPS limit...
            00000001407D4DFD | F3:0F58D0                    | addss xmm2,xmm0                                |
            00000001407D4E01 | 0FC6D2 00                    | shufps xmm2,xmm2,0                             |
            00000001407D4E05 | 0F51C2                       | sqrtps xmm0,xmm2                               |
            00000001407D4E08 | F3:0F5905 90309202           | mulss xmm0,dword ptr ds:[1430F7EA0]            | pFrametimeRunningSpeed->fFrametimeCriticalRunningSpeed
            00000001407D4E10 | 0F2FF8                       | comiss xmm7,xmm0                               |
         */
        internal const string PATTERN_FRAMELOCK_SPEED_FIX = "F3 0F 58 ?? 0F C6 ?? 00 0F 51 ?? F3 0F 59 ?? ?? ?? ?? ?? 0F 2F";
        internal const int PATTERN_FRAMELOCK_SPEED_FIX_OFFSET = 15;
        /**
            00000001430F7E10
            Value resolve in float table from pFrametimeRunningSpeed->fFrametimeCriticalRunningSpeed
            Hardcoded cause lazy -> if anyone knows how the table is calculated then tell me and I'll buy you a beer
         */
        private static readonly float[] PATCH_FRAMELOCK_SPEED_FIX_MATRIX = new float[]
        {
            15f,
            16f,
            16.6667f,
            18f,
            18.6875f,
            18.8516f,
            20f,
            24f,
            25f,
            27.5f,
            30f,
            32f,
            38.5f,
            40f,
            48f,
            49.5f,
            50f,
            57.2958f,
            60f,
            64f,
            66.75f,
            67f,
            78.8438f,
            80f,
            84f,
            90f,
            93.8f,
            100f,
            120f,
            127f,
            128f,
            130f,
            140f,
            144f,
            150f
        };
        internal const float PATCH_FRAMELOCK_SPEED_FIX_DEFAULT_VALUE = 30f;
        /// <summary>
        /// Finds closest valid speed fix value for a frame rate limit.
        /// </summary>
        /// <param name="frameLimit">The set frame rate limit.</param>
        /// <returns>The value of the closest speed fix.</returns>
        internal static float FindSpeedFixForRefreshRate(int frameLimit)
        {
            float idealSpeedFix = frameLimit / 2f;
            float closestSpeedFix = PATCH_FRAMELOCK_SPEED_FIX_DEFAULT_VALUE;
            foreach (float speedFix in PATCH_FRAMELOCK_SPEED_FIX_MATRIX)
            {
                if (Math.Abs(idealSpeedFix - speedFix) < Math.Abs(idealSpeedFix - closestSpeedFix))
                    closestSpeedFix = speedFix;
            }
            return closestSpeedFix;
        }


        /**
            Reference pointer pCurrentResolutionWidth to <int>iInternalGameWidth (and <int>iInternalGameHeight which is +4 bytes).
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
            Overwrite an unused one with desired new one. Some glitches, 1920x1080 and 1280x720 works best.
         */
        internal const string PATTERN_RESOLUTION_DEFAULT = "80 07 00 00 38 04 00 00 00 08 00 00 80 04 00 00"; // 1920x1080
        internal const string PATTERN_RESOLUTION_DEFAULT_720 = "00 05 00 00 D0 02 00 00 A0 05 00 00 2A 03 00 00"; // 1280x720
        internal static readonly byte[] PATCH_RESOLUTION_DEFAULT_DISABLE = new byte[8] { 0x80, 0x07, 0x00, 0x00, 0x38, 0x04, 0x00, 0x00 };
        internal static readonly byte[] PATCH_RESOLUTION_DEFAULT_DISABLE_720 = new byte[8] { 0x00, 0x05, 0x00, 0x00, 0xD0, 0x02, 0x00, 0x00 };


        /**
            Conditional jump instruction that determines if 16/9 scaling for game is enforced or not, overwrite with non conditional JMP so widescreen won't get clinched.
            0000000140129678 | 85C9                         | test ecx,ecx                                   |
            000000014012967A | 74 47                        | je sekiro.1401296C3                            | calculation for screen scaling
            000000014012967C | 47:8B94C7 1C020000           | mov r10d,dword ptr ds:[r15+r8*8+21C]           | resolution scaling calculation method within jump...
            0000000140129684 | 45:85D2                      | test r10d,r10d                                 |
            0000000140129687 | 74 3A                        | je sekiro.1401296C3                            |
         */
        internal const string PATTERN_RESOLUTION_SCALING_FIX = "85 C9 74 ?? 47 8B ?? ?? ?? ?? ?? ?? 45 ?? ?? 74";
        internal static readonly byte[] PATCH_RESOLUTION_SCALING_FIX_ENABLE = new byte[3] { 0x90, 0x90, 0xEB };  // nop; jmp
        internal static readonly byte[] PATCH_RESOLUTION_SCALING_FIX_DISABLE = new byte[3] { 0x85, 0xC9, 0x74 }; // test ecx,ecx; je


        /**
            Reference pointer pFovTableEntry to FOV entry in game FOV table that gets used in FOV calculations. Overwrite pFovTableEntry address to use a higher or lower <float>fFOV from table.
            FOV is in radians while default is 1.0deg (0.0174533rad), to increase by 25% you'd write 1.25deg (0.0218166rad) as fFov.
            0000000140739548 | F3:0F1008                 | movss xmm1,dword ptr ds:[rax]                   |
            000000014073954C | F3:0F590D 0CE79B02        | mulss xmm1,dword ptr ds:[1430F7C60]             | pFovTableEntry->fFov
            0000000140739554 | F3:0F5C4E 50              | subss xmm1,dword ptr ds:[rsi+50]                |
         */
        // credits to 'jackfuste' for original offset
        internal const string PATTERN_FOVSETTING = "F3 0F 10 08 F3 0F 59 0D ?? ?? ?? ?? F3 0F 5C 4E";
        internal const int PATTERN_FOVSETTING_OFFSET = 8;
        internal const float PATCH_FOVSETTING_DISABLE = 0.0174533f; // Rad2Deg -> 1°


        /**
           Reference pointer pPlayerStatsRelated to PlayerStats pointer, offset in struct to <int>iPlayerDeaths.
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
            Controls camera pitch. xmm4 holds new pitch from a calculation while rps+170 holds current one from mouse so we overwrite xmm4 with the old pitch value.
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
            Controls automatic camera yaw adjust on move on Z-axis. xmm0 holds new yaw while rsi+174 holds current one prior movement so we overwrite xmm0 with the old yaw value.
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
            Controls automatic camera pitch adjust on move on XY-axis. 
            Pointer in rax holds new pitch while rsi+170 holds current one prior movement so we overwrite xmm0 with the old pitch value and then overwrite [rax] with xmm0.
            Breaks Pitch on emulated controllers...
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
            Controls automatic camera yaw adjust on move on XY-axis. xmm0 new yaw while rsi+174 holds current one prior movement so we overwrite xmm0 with the old yaw value.
            000000014073B564 | E8 B7BCFFFF                | call sekiro.140737220                          | 
            000000014073B569 | F3:0F1186 74010000         | movss dword ptr ds:[rsi+174],xmm0              | camYaw, newCamYaw | code inject overwrite from here
            000000014073B571 | E9 9A020000                | jmp sekiro.14073B810                           | jump back here from code inject
         */
        // thanks to 'Cielos' for original offset
        internal const string PATTERN_CAMADJUST_YAW_XY = "E8 ?? ?? ?? ?? F3 0F 11 86 ?? ?? 00 00 E9";
        internal const int PATTERN_CAMADJUST_YAW_XY_OFFSET = 5;
        internal const int INJECT_CAMADJUST_YAW_XY_OVERWRITE_LENGTH = 8;
        internal static readonly byte[] INJECT_CAMADJUST_YAW_XY_SHELLCODE = new byte[]
        {
            0xF3, 0x0F, 0x10, 0x86, 0x74, 0x01, 0x00, 0x00, // movss xmm0,dword ptr ds:[rsi+174]
            0xF3, 0x0F, 0x11, 0x86, 0x74, 0x01, 0x00, 0x00  // movss dword ptr ds:[rsi+174],xmm0
        };


        /**
            When user presses button to lock on target but no target is in range a camera reset is triggered to center cam position. This boolean indicates if we need to reset or not.
            000000014073AD97  | C686 A3020000 01           | mov byte ptr ds:[rsi+2A3],1                    | Sets bool to indicate we need to reset camera and block user input til cam is reset
            000000014073AD9E  | F3:0F108E B4020000         | movss xmm1,dword ptr ds:[rsi+2B4]              |
         */
        internal const string PATTERN_CAMRESET_LOCKON = "C6 86 ?? ?? 00 00 ?? F3 0F 10 8E ?? ?? 00 00";
        internal const int PATTERN_CAMRESET_LOCKON_OFFSET = 6;
        internal static readonly byte[] PATCH_CAMRESET_LOCKON_DISABLE = new byte[1] { 0x00 }; // false
        internal static readonly byte[] PATCH_CAMRESET_LOCKON_ENABLE = new byte[1] { 0x01 }; // true


        /**
            Whole dragonrot routine upon death is guarded by a conditional jump, there may be some events in the game where a true death shall not increase the disease so it's skippable as a whole.
            We replace conditional jump with non-conditional one.
            00000001411891E8 | 45:33C0                    | xor r8d,r8d                                    |
            00000001411891EB | BA 27250000                | mov edx,2527                                   |
            00000001411891F0 | E8 AB8353FF                | call sekiro.1406C15A0                          |
            00000001411891F5 | 84C0                       | test al,al                                     |
            00000001411891F7 | 0F85 E6010000              | jne sekiro.1411893E3                           | handle dragonrot?
            00000001411891FD | 48:8B0D 44A09B02           | mov rcx,qword ptr ds:[143B43248]               | dragonrot routine...
            0000000141189204 | 48:85C9                    | test rcx,rcx                                   |
            0000000141189207 | 75 2E                      | jne sekiro.141189237                           |
            0000000141189209 | 48:8D0D 19929B02           | lea rcx,qword ptr ds:[143B42429]               |
            0000000141189210 | E8 5B178100                | call sekiro.14199A970                          |
            0000000141189215 | 4C:8BC8                    | mov r9,rax                                     |
            0000000141189218 | 4C:8D05 510EF601           | lea r8,qword ptr ds:[1430EA070]                |
            000000014118921F | BA B1000000                | mov edx,B1                                     |
            0000000141189224 | 48:8D0D 85216601           | lea rcx,qword ptr ds:[1427EB3B0]               |
            000000014118922B | E8 808F8000                | call sekiro.1419921B0                          |
            0000000141189230 | 48:8B0D 11A09B02           | mov rcx,qword ptr ds:[143B43248]               |
            0000000141189237 | 45:33C0                    | xor r8d,r8d                                    |
            000000014118923A | BA 28250000                | mov edx,2528                                   |
            000000014118923F | E8 5C8353FF                | call sekiro.1406C15A0                          |
            0000000141189244 | 84C0                       | test al,al                                     |
            0000000141189246 | 0F84 B2000000              | je sekiro.1411892FE                            | increase dragonrot level on NPCs?
            000000014118924C | 48:8D8424 90000000         | lea rax,qword ptr ss:[rsp+90]                  | executes after a certain deaths threshold has been reached...
         */
        internal const string PATTERN_DRAGONROT_EFFECT = "45 ?? ?? BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 85 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 85 C9 75 ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C ?? ?? 4C ?? ?? ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 45 ?? ?? BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 8D";
        internal const int PATTERN_DRAGONROT_EFFECT_OFFSET = 13;
        internal static readonly byte[] PATCH_DRAGONROT_EFFECT_DISABLE = new byte[4] { 0x90, 0x90, 0x90, 0xE9 }; // nop; jmp
        internal static readonly byte[] PATCH_DRAGONROT_EFFECT_ENABLE = new byte[4] { 0x84, 0xC0, 0x0F, 0x85 }; // test al,al; jne


        /**
            sekiro.14066B520 is used to increase and decrease various player values, in this case it's used to decrease Sen so we skip the call.
            0000000141189044 | F344:0F2CE9                  | cvttss2si r13d,xmm1                            |
            0000000141189049 | 41:8BD5                      | mov edx,r13d                                   |
            000000014118904C | 48:8BCB                      | mov rcx,rbx                                    |
            000000014118904F | E8 CC244EFF                  | call sekiro.14066B520                          | -> ManipulatePlayerValues()
            0000000141189054 | 8BAB 60010000                | mov ebp,dword ptr ds:[rbx+160]                 |
         */
        internal const string PATTERN_DEATHPENALTIES1 = "F3 ?? 0F 2C ?? 41 ?? ?? 48 ?? ?? E8 ?? ?? ?? ?? 8B";
        internal const int PATTERN_DEATHPENALTIES1_OFFSET = 11;
        internal const int PATCH_DEATHPENALTIES1_INSTRUCTION_LENGTH = 5;
        internal static readonly byte[] PATCH_DEATHPENALTIES1_DISABLE = new byte[5] { 0x90, 0x90, 0x90, 0x90, 0x90 }; // nop
        /**
            Here ability points (AP) are decreased and virtual Sen & AP decrease is set. The later 2 values will be shown after death as an indicator on how much of each has been lost.
            0000000141189138 | 8B00                         | mov eax,dword ptr ds:[rax]                     |
            000000014118913A | 8983 60010000                | mov dword ptr ds:[rbx+160],eax                 | OnDeath() ability points (AP) decrease
            0000000141189140 | 45:2BFD                      | sub r15d,r13d                                  |
            0000000141189143 | 44:89BC24 90000000           | mov dword ptr ss:[rsp+90],r15d                 | virtual Sen decrease - shows how many Sen got lost after death
            000000014118914B | 2BE9                         | sub ebp,ecx                                    |
            000000014118914D | 89AC24 94000000              | mov dword ptr ss:[rsp+94],ebp                  | virtual AP decrease - shows how many APs got lost after death
            0000000141189154 | E8 371C73FF                  | call sekiro.1408BAD90                          |
         */
        internal const string PATTERN_DEATHPENALTIES2 = "8B ?? 89 83 ?? ?? ?? ?? 45 ?? ?? 44 89 ?? 24 ?? ?? 00 00 2B ?? 89 ?? 24 ?? ?? 00 00 E8";
        internal const int PATTERN_DEATHPENALTIES2_OFFSET = 2;
        internal const int PATCH_DEATHPENALTIES2_INSTRUCTION_LENGTH = 26;
        internal static readonly byte[] PATCH_DEATHPENALTIES2_DISABLE = new byte[26] // nop
        {
            0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            0x90, 0x90
        };


        /**
            000000014069AE2E | 0F84 DD000000                | je sekiro.14069AF11                            |
            000000014069AE34 | 84DB                         | test bl,bl                                     |
            000000014069AE36 | 0F85 D5000000                | jne sekiro.14069AF11                           | handle death increase?
            000000014069AE3C | 48:8BCF                      | mov rcx,rdi                                    |
            000000014069AE3F | E8 BCA9FEFF                  | call sekiro.140685800                          | -> IncreaseDeaths()
         */
        internal const string PATTERN_DEATHSCOUNTER = "0F 84 ?? ?? ?? ?? 84 DB 0F 85 ?? ?? ?? ?? 48 8B ?? E8";
        internal const int PATTERN_DEATHSCOUNTER_OFFSET = 6;
        internal static readonly byte[] PATCH_DEATHSCOUNTER_DISABLE = new byte[4] { 0x90, 0x90, 0x90, 0xE9 }; // nop; jmp
        internal static readonly byte[] PATCH_DEATHSCOUNTER_ENABLE = new byte[4] { 0x84, 0xDB, 0x0F, 0x85 }; // test bl,bl; jne


        /**
            Reference pointer pTimeRelated to TimescaleManager pointer, offset in struct to <float>fTimescale which acts as a global speed scale for almost all ingame calculations.
            0000000141149E87 | 48:8B05 3A24B402          | mov rax,qword ptr ds:[143C8C2C8]                | pTimeRelated->[TimescaleManager+0x360]->fTimescale
            0000000141149E8E | F3:0F1088 60030000        | movss xmm1,dword ptr ds:[rax+360]               | offset TimescaleManager->fTimescale
            0000000141149E96 | F3:0F5988 68020000        | mulss xmm1,dword ptr ds:[rax+268]               |
         */
        // credits to 'Zullie the Witch' for original offset
        internal const string PATTERN_TIMESCALE = "48 8B 05 ?? ?? ?? ?? F3 0F 10 88 ?? ?? ?? ?? F3 0F";
        internal const int PATTERN_TIMESCALE_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_TIMESCALE_POINTER_OFFSET_OFFSET = 11;


        /**
            Reference pointer pPlayerStructRelated1 to 4 more pointers up to player data class, offset in struct to <float>fTimescalePlayer which acts as a speed scale for the player character.
            00000001406BF1D7 | 48:8B1D 128C4A03             | mov rbx,qword ptr ds:[143B67DF0]               | pPlayerStructRelated1->[pPlayerStructRelated2+0x88]->[pPlayerStructRelated3+0x1FF8]->[pPlayerStructRelated4+0x28]->[pPlayerStructRelated5+0xD00]->fTimescalePlayer
            00000001406BF1DE | 48:85DB                      | test rbx,rbx                                   |
            00000001406BF1E1 | 74 3C                        | je sekiro.1406BF21F                            |
            00000001406BF1E3 | 8B17                         | mov edx,dword ptr ds:[rdi]                     |
            00000001406BF1E5 | 81FA 10270000                | cmp edx,2710                                   |
         */
        // credits to 'Zullie the Witch' for original offset
        internal const string PATTERN_TIMESCALE_PLAYER = "48 8B 1D ?? ?? ?? ?? 48 85 DB 74 ?? 8B ?? 81 FA";
        internal const int PATTERN_TIMESCALE_PLAYER_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_TIMESCALE_POINTER2_OFFSET = 0x88;
        internal const int PATTERN_TIMESCALE_POINTER3_OFFSET = 0x1FF8;
        internal const int PATTERN_TIMESCALE_POINTER4_OFFSET = 0x28;
        internal const int PATTERN_TIMESCALE_POINTER5_OFFSET = 0xD00;
    }
}
