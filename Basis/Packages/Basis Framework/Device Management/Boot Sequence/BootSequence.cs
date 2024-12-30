using Basis.Scripts.Addressable_Driver.Resource;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Basis.Scripts.Boot_Sequence
{
    public static class BootSequence
    {
        public static GameObject LoadedBootManager;
        public static string BootManager = "BootManager";
        public static bool HasEvents = false;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnBeforeSceneLoadRuntimeMethod()
        {
            AsyncOperationHandle<IResourceLocator> Address = Addressables.InitializeAsync(false);
            Address.Completed += OnAddressablesInitializationComplete;
            HasEvents = true;
        }
        private static async void OnAddressablesInitializationComplete(AsyncOperationHandle<IResourceLocator> obj)
        {
           await OnAddressablesInitializationComplete();
        }
        public static async Task OnAddressablesInitializationComplete()
        {
            ChecksRequired Required = new ChecksRequired
            {
                UseContentRemoval = false,
                DisableAnimatorEvents = false
            };
            var data = await AddressableResourceProcess.LoadAsGameObjectsAsync(BootManager, new UnityEngine.ResourceManagement.ResourceProviders.InstantiationParameters(), Required);
            List<GameObject> Gameobjects = data.Item1;
            if (Gameobjects.Count != 0)
            {
                foreach (GameObject gameObject in Gameobjects)
                {
                    gameObject.name = BootManager;
                }
            }
            else
            {
                Debug.LogError("Missing " + BootManager);
            }
        }
    }
}