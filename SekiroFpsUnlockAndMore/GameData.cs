using System;

namespace SekiroFpsUnlockAndMore
{
    internal class GameData
    {
        internal const string PROCESS_NAME = "sekiro";
        internal const string PROCESS_TITLE = "Sekiro";
        internal const string PROCESS_DESCRIPTION = "Shadows Die Twice";
        internal const string PROCESS_EXE_VERSION = "1.5.0.0";
        internal static readonly string[] PROCESS_EXE_VERSION_SUPPORTED = new string[3]
        {
            "1.4.0.0",
            "1.3.0.0",
            "1.2.0.0"
        };


        /**
            <float>fFrameTick determines default frame rate limit in seconds.
            0000000141161FCD | C743 18 8988883C             | mov dword ptr ds:[rbx+18],3C888889                    | fFrameTick
            0000000141161FD4 | 4C:89AB 70020000             | mov qword ptr ds:[rbx+270],r13                        |

            0000000141161694 (Version 1.2.0.0)
         */
        internal const string PATTERN_FRAMELOCK = "88 88 3C 4C 89 AB"; // first byte can can be 88/90 instead of 89 due to precision loss on floating point numbers
        internal const int PATTERN_FRAMELOCK_OFFSET = -1; // offset to byte array from found position
        internal const string PATTERN_FRAMELOCK_FUZZY = "C7 43 ?? ?? ?? ?? ?? 4C 89 AB";
        internal const int PATTERN_FRAMELOCK_FUZZY_OFFSET = 3;


        /**
            Reference pointer pFrametimeRunningSpeed to speed table entry that gets used in calculations. 
            Add or remove multiplications of 4bytes to pFrametimeRunningSpeed address to use a higher or lower <float>fFrametimeCriticalRunningSpeed from table.
            fFrametimeCriticalRunningSpeed should be roughly half the frame rate: 30 @ 60FPS limit, 50 @ 100FPS limit...
            00000001407D4F3D | F3:0F58D0                    | addss xmm2,xmm0                                       |
            00000001407D4F41 | 0FC6D2 00                    | shufps xmm2,xmm2,0                                    |
            00000001407D4F45 | 0F51C2                       | sqrtps xmm0,xmm2                                      |
            00000001407D4F48 | F3:0F5905 E8409202           | mulss xmm0,dword ptr ds:[1430F9038]                   | pFrametimeRunningSpeed->fFrametimeCriticalRunningSpeed
            00000001407D4F50 | 0F2FF8                       | comiss xmm7,xmm0                                      |

            00000001407D4E08 (Version 1.2.0.0)
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
            000000014114B5C5 | 0F57D2                       | xorps xmm2,xmm2                                       |
            000000014114B5C8 | 890D 521B7D02                | mov dword ptr ds:[14391D120],ecx                      | iInternalGameWidth
            000000014114B5CE | 0F57C9                       | xorps xmm1,xmm1                                       |
            000000014114B5D1 | 8915 4D1B7D02                | mov dword ptr ds:[14391D124],edx                      | iInternalGameHeight

            000000014114AC88 (Version 1.2.0.0)
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
            0000000140129678 | 85C9                         | test ecx,ecx                                          |
            000000014012967A | 74 47                        | je sekiro.1401296C3                                   | calculation for screen scaling
            000000014012967C | 47:8B94C7 1C020000           | mov r10d,dword ptr ds:[r15+r8*8+21C]                  | resolution scaling calculation method within jump...
            0000000140129684 | 45:85D2                      | test r10d,r10d                                        |
            0000000140129687 | 74 3A                        | je sekiro.1401296C3                                   |
         */
        internal const string PATTERN_RESOLUTION_SCALING_FIX = "85 C9 74 ?? 47 8B ?? ?? ?? ?? ?? ?? 45 ?? ?? 74";
        internal static readonly byte[] PATCH_RESOLUTION_SCALING_FIX_ENABLE = new byte[3] { 0x90, 0x90, 0xEB };  // nop; jmp
        internal static readonly byte[] PATCH_RESOLUTION_SCALING_FIX_DISABLE = new byte[3] { 0x85, 0xC9, 0x74 }; // test ecx,ecx; je


        /**
            Reference pointer pFovTableEntry to FOV entry in game FOV table that gets used in FOV calculations. Overwrite pFovTableEntry address to use a higher or lower <float>fFOV from table.
            FOV is in radians while default is 1.0deg (0.0174533rad), to increase by 25% you'd write 1.25deg (0.0218166rad) as fFov.
            00000001407395A8 | F3:0F1008                    | movss xmm1,dword ptr ds:[rax]                         |
            00000001407395AC | F3:0F590D 44F89B02           | mulss xmm1,dword ptr ds:[1430F8DF8]                   | pFovTableEntry->fFov
            00000001407395B4 | F3:0F5C4E 50                 | subss xmm1,dword ptr ds:[rsi+50]                      |

            000000014073954C (Version 1.2.0.0)
         */
        // credits to 'jackfuste' for original offset
        internal const string PATTERN_FOVSETTING = "F3 0F 10 08 F3 0F 59 0D ?? ?? ?? ?? F3 0F 5C 4E";
        internal const int PATTERN_FOVSETTING_OFFSET = 8;
        internal const float PATCH_FOVSETTING_DISABLE = 0.0174533f; // Rad2Deg -> 1°


        /**
           Reference pointer pPlayerStatsRelated to PlayerStats pointer, offset in struct to <int>iPlayerDeaths.
            00000001407AAD51 | 0FB648 7A                    | movzx ecx,byte ptr ds:[rax+7A]                        |
            00000001407AAD55 | 888B F7000000                | mov byte ptr ds:[rbx+F7],cl                           |
            00000001407AAD5B | 48:8B05 CEDF3903             | mov rax,qword ptr ds:[143B48D30]                      |
            00000001407AAD62 | 8B88 8C000000                | mov ecx,dword ptr ds:[rax+8C]                         |
            00000001407AAD68 | 898B F8000000                | mov dword ptr ds:[rbx+F8],ecx                         |
            00000001407AAD6E | 48:8B05 BBDF3903             | mov rax,qword ptr ds:[143B48D30]                      | pPlayerStatsRelated->[PlayerStats+0x90]->iPlayerDeaths
            00000001407AAD75 | 8B88 90000000                | mov ecx,dword ptr ds:[rax+90]                         | offset pPlayerStats->iPlayerDeaths
            
            00000001407AACAF (Version 1.2.0.0)
        */
        // credits to 'Me_TheCat' for original offset
        internal const string PATTERN_PLAYER_DEATHS = "0F B6 48 ?? 88 8B ?? ?? 00 00 48 8B 05 ?? ?? ?? ?? 8B 88 ?? ?? 00 00 89 8B ?? ?? 00 00 48 8B 05 ?? ?? ?? ?? 8B 88 ?? ?? 00 00";
        internal const int PATTERN_PLAYER_DEATHS_OFFSET = 29;
        internal const int PATTERN_PLAYER_DEATHS_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_PLAYER_DEATHS_POINTER_OFFSET_OFFSET = 9;


        /**
            Reference pointer pPlayerStatsRelated to 2 more PlayerStatsRelated pointer, offset in struct to <int>iTotalKills.
            00000001407BFE25 | 48:69D8 18020000              | imul rbx,rax,218                         |
            00000001407BFE2C | 48:8B05 FD8E3803              | mov rax,qword ptr ds:[143B48D30]         | pPlayerStatsRelated->[PlayerStatsRelated1+0x08]->[PlayerStatsRelated2+0xDC]->iTotalKills
            00000001407BFE33 | 48:03D9                       | add rbx,rcx                              |
            00000001407BFE36 | 48:897C24 20                  | mov qword ptr ss:[rsp+20],rdi            |
            00000001407BFE3B | 48:8B78 08                    | mov rdi,qword ptr ds:[rax+8]             | offset PlayerStatsRelated1->PlayerStatsRelated2
            
            0000000000000000 (Version 1.2.0.0)
        */
        internal const string PATTERN_TOTAL_KILLS = "48 ?? D8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 ?? ?? 48 89 ?? ?? ?? 48 8B ?? 08";
        internal const int PATTERN_TOTAL_KILLS_OFFSET = 7;
        internal const int PATTERN_TOTAL_KILLS_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_TOTAL_KILLS_POINTER1_OFFSET = 0x0008;
        internal const int PATTERN_TOTAL_KILLS_POINTER2_OFFSET = 0x00DC;


        /**
            Controls camera pitch. xmm4 holds new pitch from a calculation while rsi+170 holds current one from mouse so we overwrite xmm4 with the old pitch value.
            000000014073AF86 | 0F29A5 70080000              | movaps xmmword ptr ss:[rbp+870],xmm4                  | code inject overwrite from here
            000000014073AF8D | 0F29A5 80080000              | movaps xmmword ptr ss:[rbp+880],xmm4                  | jump back here from code inject
            000000014073AF94 | 0F29A6 70010000              | movaps xmmword ptr ds:[rsi+170],xmm4                  | camPitch, newCamPitch
            000000014073AF9B | EB 1C                        | jmp sekiro.14073AFB9                                  |
            000000014073AF9D | F3:0F108E 74010000           | movss xmm1,dword ptr ds:[rsi+174]                     |

            000000014073AF26 (Version 1.2.0.0)
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
            000000014073AFAC | E8 6F60FFFF                  | call sekiro.140731020                                 |
            000000014073AFB1 | F3:0F1186 74010000           | movss dword ptr ds:[rsi+174],xmm0                     | camYaw, newCamYaw | code inject overwrite from here
            000000014073AFB9 | 80BE A3020000 00             | cmp byte ptr ds:[rsi+2A3],0                           | jump back here from code inject
            000000014073AFC0 | 0F84 2F020000                | je sekiro.14073B1F5                                   |

            000000014073AF51 (Version 1.2.0.0)
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
            000000014073B4D6 | F3:0F1000                    | movss xmm0,dword ptr ds:[rax]                         | newCamPitch | code inject overwrite from here
            000000014073B4DA | F3:0F1186 70010000           | movss dword ptr ds:[rsi+170],xmm0                     | camePitch
            000000014073B4E2 | F3:0F1085 E4120000           | movss xmm0,dword ptr ss:[rbp+12E4]                    | jump back here from code inject
            000000014073B4EA | E8 91BDFFFF                  | call sekiro.140737280                                 |
            000000014073B4EF | 0F28D0                       | movaps xmm2,xmm0                                      |

            000000014073B47A (Version 1.2.0.0)
         */
        // thanks to 'Cielos' for original offset
        internal const string PATTERN_CAMADJUST_PITCH_XY = "F3 ?? ?? ?? F3 ?? ?? ?? 70 01 00 00 F3 ?? ?? ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F";
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
            000000014073B5C4 | E8 B7BCFFFF                  | call sekiro.140737280                                 |
            000000014073B5C9 | F3:0F1186 74010000           | movss dword ptr ds:[rsi+174],xmm0                     | camYaw, newCamYaw | code inject overwrite from here
            000000014073B5D1 | E9 9A020000                  | jmp sekiro.14073B870                                  | jump back here from code inject

            000000014073B569 (Version 1.2.0.0)
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
            000000014073ADF7 | C686 A3020000 01             | mov byte ptr ds:[rsi+2A3],1                           | Sets bool to indicate we need to reset camera and block user input til cam is reset
            000000014073ADFE | F3:0F108E B4020000           | movss xmm1,dword ptr ds:[rsi+2B4]                     |

            000000014073AD97 (Version 1.2.0.0)
         */
        internal const string PATTERN_CAMRESET_LOCKON = "C6 86 ?? ?? 00 00 ?? F3 0F 10 8E ?? ?? 00 00";
        internal const int PATTERN_CAMRESET_LOCKON_OFFSET = 6;
        internal static readonly byte[] PATCH_CAMRESET_LOCKON_DISABLE = new byte[1] { 0x00 }; // false
        internal static readonly byte[] PATCH_CAMRESET_LOCKON_ENABLE = new byte[1] { 0x01 }; // true


        /**
            Picking up enemy loot can be automated by setting key press indicator to 1.
            0000000140910D14 | C685 30010000 01              | mov byte ptr ss:[rbp+130],1              |
            0000000140910D1B | B0 01                         | mov al,1                                 | triggers loot pickup
            0000000140910D1D | EB 09                         | jmp sekiro.140910D28                     |
            0000000140910D1F | C685 30010000 00              | mov byte ptr ss:[rbp+130],0              |
            0000000140910D26 | 32C0                          | xor al,al                                | resets loot pickup
         */
        internal const string PATTERN_AUTOLOOT = "C6 85 ?? ?? ?? ?? ?? B0 01 EB ?? C6 85 ?? ?? ?? ?? ?? 32 C0";
        internal const int PATTERN_AUTOLOOT_OFFSET = 18;
        internal static readonly byte[] PATCH_AUTOLOOT_ENABLE = new byte[2] { 0xB0, 0x01}; // mov al,1
        internal static readonly byte[] PATCH_AUTOLOOT_DISABLE = new byte[2] { 0x32, 0xC0 }; // xor al,al


        /**
            Whole dragonrot routine upon death is guarded by a conditional jump, there may be some events in the game where a true death shall not increase the disease so it's skippable as a whole.
            We replace conditional jump with non-conditional one.
            0000000141189D18 | 45:33C0                      | xor r8d,r8d                                           |
            0000000141189D1B | BA 27250000                  | mov edx,2527                                          |
            0000000141189D20 | E8 DB7853FF                  | call sekiro.1406C1600                                 |
            0000000141189D25 | 84C0                         | test al,al                                            |
            0000000141189D27 | 0F85 E6010000                | jne sekiro.141189F13                                  | handle dragonrot?
            0000000141189D2D | 48:8B0D 54A59B02             | mov rcx,qword ptr ds:[143B44288]                      | dragonrot routine...
            0000000141189D34 | 48:85C9                      | test rcx,rcx                                          |
            0000000141189D37 | 75 2E                        | jne sekiro.141189D67                                  |
            0000000141189D39 | 48:8D0D 29979B02             | lea rcx,qword ptr ds:[143B43469]                      |
            0000000141189D40 | E8 5B178100                  | call sekiro.14199B4A0                                 |
            0000000141189D45 | 4C:8BC8                      | mov r9,rax                                            |
            0000000141189D48 | 4C:8D05 5914F601             | lea r8,qword ptr ds:[1430EB1A8]                       |
            0000000141189D4F | BA B1000000                  | mov edx,B1                                            |
            0000000141189D54 | 48:8D0D 55266601             | lea rcx,qword ptr ds:[1427EC3B0]                      | 
            0000000141189D5B | E8 808F8000                  | call sekiro.141992CE0                                 |
            0000000141189D60 | 48:8B0D 21A59B02             | mov rcx,qword ptr ds:[143B44288]                      |
            0000000141189D67 | 45:33C0                      | xor r8d,r8d                                           |
            0000000141189D6A | BA 28250000                  | mov edx,2528                                          |
            0000000141189D6F | E8 8C7853FF                  | call sekiro.1406C1600                                 |
            0000000141189D74 | 84C0                         | test al,al                                            |
            0000000141189D76 | 0F84 B2000000                | je sekiro.141189E2E                                   | increase dragonrot level on NPCs?
            0000000141189D7C | 48:8D8424 90000000           | lea rax,qword ptr ss:[rsp+90]                         | executes after a certain deaths threshold has been reached...
         
            00000001411891F7 (Version 1.2.0.0)
        */
        internal const string PATTERN_DRAGONROT_EFFECT = "45 ?? ?? BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 85 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 85 C9 75 ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C ?? ?? 4C ?? ?? ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 45 ?? ?? BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 8D";
        internal const int PATTERN_DRAGONROT_EFFECT_OFFSET = 13;
        internal static readonly byte[] PATCH_DRAGONROT_EFFECT_DISABLE = new byte[4] { 0x90, 0x90, 0x90, 0xE9 }; // nop; jmp
        internal static readonly byte[] PATCH_DRAGONROT_EFFECT_ENABLE = new byte[4] { 0x84, 0xC0, 0x0F, 0x85 }; // test al,al; jne


        /**
            sekiro.14066B520 is used to increase and decrease various player values, in this case it's used to decrease Sen so we skip the call.
            0000000141189B74 | F344:0F2CE9                  | cvttss2si r13d,xmm1                                   |
            0000000141189B79 | 41:8BD5                      | mov edx,r13d                                          |
            0000000141189B7C | 48:8BCB                      | mov rcx,rbx                                           |
            0000000141189B7F | E8 FC194EFF                  | call sekiro.14066B580                                 | -> ManipulatePlayerValues()
            0000000141189B84 | 8BAB 60010000                | mov ebp,dword ptr ds:[rbx+160]                        |

            000000014118904F (Version 1.2.0.0)
         */
        internal const string PATTERN_DEATHPENALTIES1 = "F3 ?? 0F 2C ?? 41 ?? ?? 48 ?? ?? E8 ?? ?? ?? ?? 8B";
        internal const int PATTERN_DEATHPENALTIES1_OFFSET = 11;
        internal const int PATCH_DEATHPENALTIES1_INSTRUCTION_LENGTH = 5;
        internal static readonly byte[] PATCH_DEATHPENALTIES1_DISABLE = new byte[5] { 0x90, 0x90, 0x90, 0x90, 0x90 }; // nop
        /**
            Here ability points (AP) are decreased and virtual Sen & AP decrease is set. The later 2 values will be shown after death as an indicator on how much of each has been lost.
            0000000141189C68 | 8B00                         | mov eax,dword ptr ds:[rax]                            |
            0000000141189C6A | 8983 60010000                | mov dword ptr ds:[rbx+160],eax                        | OnDeath() ability points (AP) decrease
            0000000141189C70 | 45:2BFD                      | sub r15d,r13d                                         |
            0000000141189C73 | 44:89BC24 90000000           | mov dword ptr ss:[rsp+90],r15d                        | virtual Sen decrease - shows how many Sen got lost after death
            0000000141189C7B | 2BE9                         | sub ebp,ecx                                           |
            0000000141189C7D | 89AC24 94000000              | mov dword ptr ss:[rsp+94],ebp                         | virtual AP decrease - shows how many APs got lost after death
            0000000141189C84 | E8 071673FF                  | call sekiro.1408BB290                                 |

            000000014118913A (Version 1.2.0.0)
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
            000000014069AE8E | 0F84 DD000000                | je sekiro.14069AF71                                   |
            000000014069AE94 | 84DB                         | test bl,bl                                            |
            000000014069AE96 | 0F85 D5000000                | jne sekiro.14069AF71                                  | handle death increase?
            000000014069AE9C | 48:8BCF                      | mov rcx,rdi                                           |
            000000014069AE9F | E8 BCA9FEFF                  | call sekiro.140685860                                 | -> IncreaseDeaths()

            000000014069AE36 (Version 1.2.0.0)
         */
        internal const string PATTERN_DEATHSCOUNTER = "0F 84 ?? ?? ?? ?? 84 DB 0F 85 ?? ?? ?? ?? 48 8B ?? E8";
        internal const int PATTERN_DEATHSCOUNTER_OFFSET = 6;
        internal static readonly byte[] PATCH_DEATHSCOUNTER_DISABLE = new byte[4] { 0x90, 0x90, 0x90, 0xE9 }; // nop; jmp
        internal static readonly byte[] PATCH_DEATHSCOUNTER_ENABLE = new byte[4] { 0x84, 0xDB, 0x0F, 0x85 }; // test bl,bl; jne


        /**
            Whenever we upgrade a prosthetic or learn an ability the following function block will get called. 
            We inject a check to determine if case is prosthetic and set register affecting SkillEffect4 to 1 so that the upgrade increases our maximum spirit emblem capacity.
            Type for struct SKILL_PARAM_ST is defined below.
            0000000140A84C29 | 48:85C0                      | test rax,rax                                          |
            0000000140A84C2C | 74 4A                        | je sekiro.140A84C78                                   | IncreaseSkill4OnUpgrade ?
            0000000140A84C2E | 0FB650 37                    | movzx edx,byte ptr ds:[rax+37]                        | get SKILL_PARAM_ST.SkillEffect4 to edx | code inject overwrite from here
            0000000140A84C32 | 85D2                         | test edx,edx                                          | check if edx is 0
            0000000140A84C34 | 74 42                        | je sekiro.140A84C78                                   | if 0 jump here | jump back here from code inject
            0000000140A84C36 | 48:8B0D F3400C03             | mov rcx,qword ptr ds:[143B48D30]                      | increase skill4 on upgrade routine
            0000000140A84C3D | 48:8B49 08                   | mov rcx,qword ptr ds:[rcx+8]                          |
            0000000140A84C41 | 48:85C9                      | test rcx,rcx                                          |
            0000000140A84C44 | 74 32                        | je sekiro.140A84C78                                   |
            0000000140A84C46 | 48:81C1 46010000             | add rcx,146                                           |
            0000000140A84C4D | 66:0111                      | add word ptr ds:[rcx],dx                              | increases Skill4 on upgrade, will get skipped if edx == 0

            0000000000000000 (Version 1.2.0.0)

            [StructLayout(LayoutKind.Explicit, Size = 0x0060)]
            private struct SKILL_PARAM_ST
            {
                [FieldOffset(0x0030)]
                private Int32 SkillFamily;      // (Unk6) 2700000 for prosthetic upgrades

                [FieldOffset(0x0037)]
                private UInt16 SkillEffect4;    // (Unk10) controls how much spirit emblem capacity rises on acquisition of skill/upgrade
            }
         */
        internal const string PATTERN_EMBLEMUPGRADE = "48 85 C0 74 ?? 0F B6 50 37 85 D2 74 ?? 48 8B 0D";
        internal const int PATTERN_EMBLEMUPGRADE_OFFSET = 5;
        internal const int INJECT_EMBLEMUPGRADE_OVERWRITE_LENGTH = 6;
        internal static readonly byte[] INJECT_EMBLEMUPGRADE_SHELLCODE = new byte[]
        {
            0x81, 0x78, 0x30, 0xE0, 0x32, 0x29, 0x00,   // cmp dword ptr ds:[rax+30],2932E0     | if (SKILL_PARAM_ST.SkillFamily == 2700000)
            0x75, 0x07,                                 // jne +7                               | {
            0xBA, 0x01, 0x00, 0x00, 0x00,               // mov edx,1                            | edx = 1
            0xEB, 0x04,                                 // jmp +4                               | } else {
            0x0F, 0xB6, 0x50, 0x37,                     // movzx edx,byte ptr ds:[rax+37]       | edx = SKILL_PARAM_ST.SkillEffect4 }
            0x85, 0xD2                                  // test edx,edx                         | check if edx is 0
        };


        /**
            Reference pointer pTimeRelated to TimescaleManager pointer, offset in struct to <float>fTimescale which acts as a global speed scale for almost all ingame calculations.
            000000014114A7C7 | 48:8B05 3A2BB402             | mov rax,qword ptr ds:[143C8D308]                      | pTimeRelated->[TimescaleManager+0x360]->fTimescale
            000000014114A7CE | F3:0F1088 60030000           | movss xmm1,dword ptr ds:[rax+360]                     | offset TimescaleManager->fTimescale
            000000014114A7D6 | F3:0F5988 68020000           | mulss xmm1,dword ptr ds:[rax+268]                     |

            0000000141149E87 (Version 1.2.0.0)
         */
        // credits to 'Zullie the Witch' for original offset
        internal const string PATTERN_TIMESCALE = "48 8B 05 ?? ?? ?? ?? F3 0F 10 88 ?? ?? ?? ?? F3 0F";
        internal const int PATTERN_TIMESCALE_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_TIMESCALE_POINTER_OFFSET_OFFSET = 11;


        /**
            Reference pointer pPlayerStructRelated1 to 4 more pointers up to player data class, offset in struct to <float>fTimescalePlayer which acts as a speed scale for the player character.
            00000001406BF237 | 48:8B1D F29B4A03             | mov rbx,qword ptr ds:[143B68E30]                      | pPlayerStructRelated1->[pPlayerStructRelated2+0x88]->[pPlayerStructRelated3+0x1FF8]->[pPlayerStructRelated4+0x28]->[pPlayerStructRelated5+0xD00]->fTimescalePlayer
            00000001406BF23E | 48:85DB                      | test rbx,rbx                                          |
            00000001406BF241 | 74 3C                        | je sekiro.1406BF27F                                   |
            00000001406BF243 | 8B17                         | mov edx,dword ptr ds:[rdi]                            |
            00000001406BF245 | 81FA 10270000                | cmp edx,2710                                          |

            00000001406BF1D7 (Version 1.2.0.0)
         */
        // credits to 'Zullie the Witch' for original offset
        internal const string PATTERN_TIMESCALE_PLAYER = "48 8B 1D ?? ?? ?? ?? 48 85 DB 74 ?? 8B ?? 81 FA";
        internal const int PATTERN_TIMESCALE_PLAYER_INSTRUCTION_LENGTH = 7;
        internal const int PATTERN_TIMESCALE_POINTER2_OFFSET = 0x0088;
        internal const int PATTERN_TIMESCALE_POINTER3_OFFSET = 0x1FF8;
        internal const int PATTERN_TIMESCALE_POINTER4_OFFSET = 0x0028;
        internal const int PATTERN_TIMESCALE_POINTER5_OFFSET = 0x0D00;
    }
}
