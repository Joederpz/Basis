using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Drivers;
using Basis.Scripts.TransformBinders.BoneControl;
using UnityEngine;

namespace Basis.Scripts.TransformBinders
{
public class BasisLockToBone : MonoBehaviour
{
    public BasisLocalBoneDriver CharacterTransformDriver;
    public BasisBoneControl BoneControl;
    public BasisBoneTrackedRole Role = BasisBoneTrackedRole.Head;
    public bool hasCharacterTransformDriver = false;
    public bool HasBoneControl = false;
    public bool HasEvent = false;
    public void Initialize(BasisLocalPlayer LocalPlayer)
    {
        if (LocalPlayer != null)
        {
            CharacterTransformDriver = LocalPlayer.LocalBoneDriver;
            if (CharacterTransformDriver == null)
            {
                hasCharacterTransformDriver = false;
                Debug.LogError("Missing CharacterTransformDriver");
            }
            else
            {
                hasCharacterTransformDriver = true;
                HasBoneControl = CharacterTransformDriver.FindBone(out BoneControl, Role);
            }
        }
        else
        {
            Debug.LogError("Missing LocalPlayer");
        }
        if (HasEvent == false)
        {
            CharacterTransformDriver.ReadyToRead.AddAction(99,Simulation);
            HasEvent = true;
        }
    }
    public void OnDestroy()
    {
        if (CharacterTransformDriver != null)
        {
            if (HasEvent)
            {
                CharacterTransformDriver.ReadyToRead.RemoveAction(99,Simulation);
                HasEvent = false;
            }
        }
    }
    void Simulation()
    {
        if (hasCharacterTransformDriver && HasBoneControl)
        {
            transform.SetPositionAndRotation(BoneControl.OutgoingWorldData.position, BoneControl.OutgoingWorldData.rotation);
        }
    }
}
}