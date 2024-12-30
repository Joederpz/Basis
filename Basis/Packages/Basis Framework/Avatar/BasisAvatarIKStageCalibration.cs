using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Device_Management;
using Basis.Scripts.Device_Management.Devices;
using Basis.Scripts.TransformBinders.BoneControl;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Basis.Scripts.Avatar
{
    public static partial class BasisAvatarIKStageCalibration
    {
        private static List<BasisBoneTrackedRole> GetAllRoles()
        {
            List<BasisBoneTrackedRole> rolesToDiscover = new List<BasisBoneTrackedRole>();
            foreach (BasisBoneTrackedRole role in Enum.GetValues(typeof(BasisBoneTrackedRole)))
            {
                rolesToDiscover.Add(role);
            }
            // Create a dictionary for quick index lookup
            Dictionary<BasisBoneTrackedRole, int> orderLookup = new Dictionary<BasisBoneTrackedRole, int>();
            for (int i = 0; i < desiredOrder.Length; i++)
            {
                orderLookup[desiredOrder[i]] = i;
            }

            // Assign a large index value to roles not in the desired order
            int largeIndex = desiredOrder.Length;

            // Sort the list based on the desired order
            rolesToDiscover.Sort((x, y) =>
            {
                int indexX = orderLookup.ContainsKey(x) ? orderLookup[x] : largeIndex;
                int indexY = orderLookup.ContainsKey(y) ? orderLookup[y] : largeIndex;
                return indexX.CompareTo(indexY);
            });

            return rolesToDiscover;
        }
        public static void FullBodyCalibration()
        {
            HasFBIKTrackers = false;
            BasisHeightDriver.SetPlayersEyeHeight(BasisLocalPlayer.Instance);
            BasisDeviceManagement.UnassignFBTrackers();
            BasisLocalPlayer.Instance.LocalBoneDriver.SimulateAndApplyWithoutLerp();

            //now that we have latest * scale we can run calibration
            BasisLocalPlayer.Instance.AvatarDriver.PutAvatarIntoTPose();
            List<BasisBoneTrackedRole> rolesToDiscover = GetAllRoles();
            List<BasisBoneTrackedRole> trackInputRoles = new List<BasisBoneTrackedRole>();
            int count = rolesToDiscover.Count;
            for (int Index = 0; Index < count; Index++)
            {
                BasisBoneTrackedRole Role = rolesToDiscover[Index];
                if (BasisBoneTrackedRoleCommonCheck.CheckItsFBTracker(Role))
                {
                    trackInputRoles.Add(Role);
                }
            }
            List<CalibrationConnector> connectors = new List<CalibrationConnector>();
            int AllInputDevicesCount = BasisDeviceManagement.Instance.AllInputDevices.Count;
            for (int Index = 0; Index < AllInputDevicesCount; Index++)
            {
                BasisInput baseInput = BasisDeviceManagement.Instance.AllInputDevices[Index];
                if (baseInput.TryGetRole(out BasisBoneTrackedRole role))
                {
                    if (BasisBoneTrackedRoleCommonCheck.CheckItsFBTracker(role))
                    {
                        //in use un assign first
                        baseInput.UnAssignFullBodyTrackers();
                        CalibrationConnector calibrationConnector = new CalibrationConnector
                        {
                            BasisInput = baseInput,
                            Distance = float.MaxValue
                        };
                        connectors.Add(calibrationConnector);
                    }
                }
                else//no assigned role
                {
                    CalibrationConnector calibrationConnector = new CalibrationConnector
                    {
                        BasisInput = baseInput,
                        Distance = float.MaxValue
                    };
                    //tracker was a uncalibrated type
                    connectors.Add(calibrationConnector);
                }
            }
            List<BasisTrackerMapping> boneTransformMappings = new List<BasisTrackerMapping>();
            int Count = trackInputRoles.Count;
            Dictionary<BasisBoneTrackedRole, Transform> StoredRolesTransforms = GetAllRolesAsTransform();
            for (int Index = 0; Index < Count; Index++)
            {
                BasisBoneTrackedRole role = trackInputRoles[Index];
                if (BasisLocalPlayer.Instance.LocalBoneDriver.FindBone(out BasisBoneControl control, role))
                {
                    float ScaledDistance = MaxDistanceBeforeMax(role) * BasisLocalPlayer.Instance.EyeRatioAvatarToAvatarDefaultScale;
                    Debug.Log("Using a scaler of  " + BasisLocalPlayer.Instance.EyeRatioAvatarToAvatarDefaultScale + " leading to a scaled Distance of " + ScaledDistance);
                    if (StoredRolesTransforms.TryGetValue(role, out Transform Transform))
                    {
                        BasisTrackerMapping mapping = new BasisTrackerMapping(control, Transform, role, connectors, ScaledDistance);
                        boneTransformMappings.Add(mapping);
                    }
                }
                else
                {
                    Debug.LogError("Missing bone control for role " + role);
                }
            }
            List<BasisBoneTrackedRole> roles = new List<BasisBoneTrackedRole>();
            List<BasisInput> BasisInputs = new List<BasisInput>();
            int cachedCount = boneTransformMappings.Count;
            // Find optimal matches
            for (int Index = 0; Index < cachedCount; Index++)
            {
                BasisTrackerMapping mapping = boneTransformMappings[Index];
                if (mapping.TargetControl != null)
                {
                    RunThroughConnectors(mapping, ref BasisInputs, ref roles);
                }
                else
                {
                    Debug.LogError("Missing Tracker for index " + Index + " with ID " + mapping);
                }
            }
            //do the roles after to stop the animator switch issue
            BasisLocalPlayer.Instance.LocalBoneDriver.CalculateHeading();

            BasisLocalPlayer.Instance.AvatarDriver.ResetAvatarAnimator();
            BasisLocalPlayer.Instance.AvatarDriver.CalibrateRoles();//not needed but still doing just incase
            BasisLocalPlayer.Instance.AvatarDriver.AnimatorDriver.AssignHipsFBTracker();
        }
        public static bool HasFBIKTrackers = false;
        public static void RunThroughConnectors(BasisTrackerMapping mapping, ref List<BasisInput> BasisInputs, ref List<BasisBoneTrackedRole> roles)
        {
            // List to store the calibration actions
            List<Action> calibrationActions = new List<Action>();

            int CandidateCount = mapping.Candidates.Count;
            for (int Index = 0; Index < CandidateCount; Index++)
            {
                CalibrationConnector Connector = mapping.Candidates[Index];
                if (BasisInputs.Contains(Connector.BasisInput) == false)
                {
                    if (roles.Contains(mapping.BasisBoneControlRole) == false)
                    {
                        roles.Add(mapping.BasisBoneControlRole);
                        BasisInputs.Add(Connector.BasisInput);
                        // Store the calibration action instead of executing it directly
                        calibrationActions.Add(() =>
                        {

                            HasFBIKTrackers = true;
                            Connector.BasisInput.ApplyTrackerCalibration(mapping.BasisBoneControlRole);
                        });

                        // Once we found a valid connector, we can stop the search
                        break;
                    }
                    else
                    {
                        //Debug.Log("we have already assigned role " + mapping.BasisBoneControlRole);
                    }
                }
                else
                {
                    //Debug.Log("Already assigned " + Connector.Tracker);
                }
            }

            // Execute all stored calibration actions
            for (int Index = 0; Index < calibrationActions.Count; Index++)
            {
                Action action = calibrationActions[Index];
                action();
            }
        }
        public static Dictionary<BasisBoneTrackedRole, Transform> GetAllRolesAsTransform()
        {
            Common.BasisTransformMapping Mapping = BasisLocalPlayer.Instance.AvatarDriver.References;
            Dictionary<BasisBoneTrackedRole, Transform> transforms = new Dictionary<BasisBoneTrackedRole, Transform>
    {
        { BasisBoneTrackedRole.Hips,Mapping.Hips },
        { BasisBoneTrackedRole.Spine, Mapping.spine },
        { BasisBoneTrackedRole.Chest, Mapping.chest },
    //    { BasisBoneTrackedRole.Upperchest, BasisLocalPlayer.Instance.AvatarDriver.References.Upperchest },
        { BasisBoneTrackedRole.Neck, Mapping.neck },
        { BasisBoneTrackedRole.Head, Mapping.head },
       // { BasisBoneTrackedRole.CenterEye, LeftEye },  // Assuming "CenterEye" means LeftEye; adjust as needed
       // { BasisBoneTrackedRole.RightEye, RightEye },   // Add these based on your actual structure

        { BasisBoneTrackedRole.LeftShoulder, Mapping.leftShoulder },
        { BasisBoneTrackedRole.LeftUpperArm, Mapping.leftUpperArm },
        { BasisBoneTrackedRole.LeftLowerArm, Mapping.leftLowerArm },
        { BasisBoneTrackedRole.LeftHand, Mapping.leftHand },

        { BasisBoneTrackedRole.RightShoulder, Mapping.RightShoulder },
        { BasisBoneTrackedRole.RightUpperArm,Mapping. RightUpperArm },
        { BasisBoneTrackedRole.RightLowerArm, Mapping.RightLowerArm },
        { BasisBoneTrackedRole.RightHand, Mapping.rightHand },

        { BasisBoneTrackedRole.LeftUpperLeg,Mapping.LeftUpperLeg },
        { BasisBoneTrackedRole.LeftLowerLeg,Mapping. LeftLowerLeg },
        { BasisBoneTrackedRole.LeftFoot, Mapping.leftFoot },
        { BasisBoneTrackedRole.LeftToes,Mapping. leftToes },

        { BasisBoneTrackedRole.RightUpperLeg, Mapping.RightUpperLeg },
        { BasisBoneTrackedRole.RightLowerLeg,Mapping. RightLowerLeg },
        { BasisBoneTrackedRole.RightFoot, Mapping.rightFoot },
        { BasisBoneTrackedRole.RightToes,Mapping. rightToes },
            };

            return transforms;
        }
        /// <summary>
        ///  = 0.4f;
        /// </summary>
        public static float MaxDistanceBeforeMax(BasisBoneTrackedRole role)
        {

            switch (role)
            {
                case BasisBoneTrackedRole.Chest:
                    return 0.45f;
                case BasisBoneTrackedRole.Hips:
                    return 0.45f;

                case BasisBoneTrackedRole.LeftLowerLeg:
                    return 0.5f;
                case BasisBoneTrackedRole.RightLowerLeg:
                    return 0.5f;

                case BasisBoneTrackedRole.LeftFoot:
                    return 0.5f;
                case BasisBoneTrackedRole.RightFoot:
                    return 0.5f;

                case BasisBoneTrackedRole.LeftShoulder:
                    return 0.4f;
                case BasisBoneTrackedRole.RightShoulder:
                    return 0.4f;

                case BasisBoneTrackedRole.LeftLowerArm:
                    return 0.6f;
                case BasisBoneTrackedRole.RightLowerArm:
                    return 0.6f;

                case BasisBoneTrackedRole.LeftHand:
                    return 0.4f;
                case BasisBoneTrackedRole.RightHand:
                    return 0.4f;

                case BasisBoneTrackedRole.LeftToes:
                    return 0.4f;
                case BasisBoneTrackedRole.RightToes:
                    return 0.4f;
                default:
                    Console.WriteLine("Unknown role " + role);
                    return 0;
            }
        }
    }
}