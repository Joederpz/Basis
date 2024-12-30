using Basis.Scripts.Addressable_Driver.Resource;
using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.TransformBinders.BoneControl;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Basis.Scripts.UI.NamePlate
{
    public class BasisRemoteNamePlate : BasisNamePlate
    {
        public static async Task LoadRemoteNamePlate(BasisRemotePlayer Player, string RemoteNamePlate = "Assets/UI/Prefabs/NamePlate.prefab")
        {
            ChecksRequired Required = new ChecksRequired
            {
                UseContentRemoval = false,
                DisableAnimatorEvents = false
            };
            var data = await AddressableResourceProcess.LoadAsGameObjectsAsync(RemoteNamePlate, new UnityEngine.ResourceManagement.ResourceProviders.InstantiationParameters(), Required);
            List<GameObject> Gameobjects = data.Item1;
            if (Gameobjects.Count != 0)
            {
                foreach (GameObject gameObject in Gameobjects)
                {
                    if (gameObject.TryGetComponent(out BasisRemoteNamePlate BasisRemoteNamePlate))
                    {
                        if (Player == null)
                        {
                            Destroy(gameObject);
                            return;
                        }
                        BasisRemoteNamePlate.transform.SetParent(Player.transform, false);
                        if (Player.RemoteBoneDriver.FindBone(out BasisBoneControl Hips, BasisBoneTrackedRole.Hips))
                        {
                            BasisRemoteNamePlate.Initalize(Hips, Player);
                        }
                    }
                }
            }
        }
    }
}