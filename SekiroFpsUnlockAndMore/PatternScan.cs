using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SekiroFpsUnlockAndMore
{
    class PatternScan
    {
        private long dwStart = 0;
        private byte[] bData;

        /// <summary>
        /// Initialize PatternScanner and read all memory from process.
        /// </summary>
        /// <param name="hProcess">Handle to the process in whose memory pattern will be searched for.</param>
        /// <param name="pModule">Module which will be searched for the pattern.</param>
        internal PatternScan(IntPtr hProcess, ProcessModule pModule)
        {
            if (IntPtr.Size == 4)
                dwStart = (uint)pModule.BaseAddress;
            else if (IntPtr.Size == 8)
                dwStart = (long)pModule.BaseAddress;
            int nSize = pModule.ModuleMemorySize;
            bData = new byte[nSize];

            if (!ReadProcessMemory(hProcess, dwStart, bData, nSize, out IntPtr lpNumberOfBytesRead))
            {
                MainWindow.LogToFile("Could not read memory in PatternScan()!");
                return;
            }
            if (lpNumberOfBytesRead.ToInt64() != nSize || bData == null || bData.Length == 0)
            {
                MainWindow.LogToFile("ReadProcessMemory error in PatternScan()!");
                return;
            }
        }

        ~PatternScan()
        {
            bData = null;
            GC.Collect();
        }

        /// <summary>
        /// Finds a pattern or signature inside initialized process.
        /// </summary>
        /// <param name="szPattern">A character-delimited string representing the pattern to be found, '??' act as a wildcard.</param>
        /// <param name="cDelimiter">Determines how the string will be split. Defaults is ' '.</param>
        /// <returns>The address of the beginning of the pattern if found, 0 if not found.</returns>
        internal Int64 FindPattern(string szPattern, char cDelimiter = ' ')
        {
            string[] saPattern = szPattern.Split(cDelimiter);
            string szMask = "";
            for (int i = 0; i < saPattern.Length; i++)
            {
                if (saPattern[i] == "??")
                {
                    szMask += "?";
                    saPattern[i] = "0";
                }
                else szMask += "x";
            }
            byte[] bPattern = new byte[saPattern.Length];
            for (int i = 0; i < saPattern.Length; i++)
                bPattern[i] = Convert.ToByte(saPattern[i], 0x10);

            if (bPattern == null || bPattern.Length == 0)
                throw new ArgumentException("Pattern's length is zero!");
            if (bPattern.Length != szMask.Length)
                throw new ArgumentException("Pattern's bytes and szMask must be of the same size!");

            long ix;
            int iy;
            bool bFound = false;
            int patternLength = bPattern.Length;
            int dataLength = bData.Length - patternLength;

            for (ix = 0; ix < dataLength; ix++)
            {
                bFound = true;
                for (iy = 0; iy < patternLength; iy++)
                {
                    if (szMask[iy] != 'x' || bPattern[iy] == bData[ix + iy]) continue;
                    bFound = false;
                    break;
                }

                if (bFound)
                    return Convert.ToInt64((long) dwStart + ix);
            }

            return 0;
        }


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
           IntPtr hProcess,
           Int64 lpBaseAddress,
           [Out] Byte[] lpBuffer,
           Int64 dwSize,
           out IntPtr lpNumberOfBytesRead);
    }
}
