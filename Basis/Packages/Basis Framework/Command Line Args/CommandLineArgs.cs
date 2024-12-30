using Basis.Scripts.Device_Management;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Basis.Scripts.Command_Line_Args
{
    public static class CommandLineArgs
    {
        private const string DisableFlag = "--disable-";
        private const string ForceFlag = "--force-";
        /// <summary>
        /// Initializes the command line argument handling for disabling specific device manager solutions.
        /// </summary>
        public static void Initialize(string[] BakedIn,out string ForcedDeviceManager)
        {
            string[] args = Environment.GetCommandLineArgs();
            List<string> StringArgs = args.ToList();
            StringArgs.AddRange(BakedIn);
            // Process each argument to identify and disable the appropriate device manager solutions
            foreach (string arg in StringArgs.Where(a => a.StartsWith(DisableFlag, StringComparison.InvariantCultureIgnoreCase)))
            {
                string replacement = arg.Substring(DisableFlag.Length);
                DisableDeviceManagerSolution(replacement);
            }

            foreach (string arg in StringArgs.Where(a => a.StartsWith(ForceFlag, StringComparison.InvariantCultureIgnoreCase)))
            {
                ForcedDeviceManager = arg.Substring(ForceFlag.Length);
                return;
            }
            ForcedDeviceManager = string.Empty;
        }

        /// <summary>
        /// Disables the device manager solution for the specified booted mode.
        /// </summary>
        /// <param name="mode">The BasisBootedMode to disable.</param>
        private static void DisableDeviceManagerSolution(string mode)
        {
            try
            {
                BasisDeviceManagement.Instance.BasisXRManagement.DisableDeviceManagerSolution(mode);
                Debug.Log($"Device manager solution for mode {mode} has been successfully disabled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to disable device manager solution for mode {mode}: {ex.Message}");
            }
        }
    }
}