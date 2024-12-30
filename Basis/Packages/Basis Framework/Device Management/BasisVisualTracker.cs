using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Device_Management.Devices;
using UnityEngine;
using UnityEngine.Events;

namespace Basis.Scripts.Device_Management
{
    public class BasisVisualTracker : MonoBehaviour
    {
        public BasisInput BasisInput;
        public UnityEvent TrackedSetup = new UnityEvent();
        public Quaternion ModelRotationOffset = Quaternion.identity;
        public Vector3 ModelPositionOffset = Vector3.zero;
        public bool HasEvents = false;
        public Vector3 ScaleOfModel = Vector3.one;
        public void Initialization(BasisInput basisInput)
        {
            if (basisInput != null)
            {
                BasisInput = basisInput;
                UpdateVisualSizeAndOffset();
                if (HasEvents == false)
                {
                    BasisLocalPlayer.Instance.OnLocalAvatarChanged += UpdateVisualSizeAndOffset;
                    BasisLocalPlayer.Instance.OnPlayersHeightChanged += StartWaitAndSetUILocation;
                    HasEvents = true;
                }
                TrackedSetup.Invoke();
            }
        }
        public void OnDestroy()
        {
            if (HasEvents)
            {
                BasisLocalPlayer.Instance.OnLocalAvatarChanged -= UpdateVisualSizeAndOffset;
                BasisLocalPlayer.Instance.OnPlayersHeightChanged -= StartWaitAndSetUILocation;
                HasEvents = false;
            }
        }
        public void StartWaitAndSetUILocation()
        {
            UpdateVisualSizeAndOffset();
        }
        public void UpdateVisualSizeAndOffset()
        {
            gameObject.transform.localScale = ScaleOfModel * BasisLocalPlayer.Instance.EyeRatioAvatarToAvatarDefaultScale;
            gameObject.transform.SetLocalPositionAndRotation(ModelPositionOffset * BasisLocalPlayer.Instance.EyeRatioPlayerToDefaultScale, ModelRotationOffset);
        }
    }
}