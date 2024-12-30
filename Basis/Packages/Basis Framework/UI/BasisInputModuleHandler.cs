using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Device_Management;
using Basis.Scripts.Device_Management.Devices.Desktop;
using Basis.Scripts.Virtual_keyboard;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Basis.Scripts.UI
{
    public class BasisInputModuleHandler : BaseInputModule
    {
        public EventSystem EventSystem;
        private InputAction tabAction;
        private InputAction enterAction;
        private InputAction keypadEnterAction;

        public TMP_InputField CurrentSelectedTMP_InputField;
        public InputField CurrentSelectedInputField;
        public bool HasHoverONInput = false;
        public bool ForceKeyboard = false;
        public BasisUIRaycastProcess basisUIRaycastProcess = new BasisUIRaycastProcess();
        public static BasisInputModuleHandler Instance;
        protected override void OnEnable()
        {
            base.OnEnable();
            Instance = this;
            // Initialize the input actions for Tab and Enter keys
            tabAction = new InputAction(binding: "<Keyboard>/tab");
            tabAction.performed += OnTabPerformed;
            tabAction.Enable();

            enterAction = new InputAction(binding: "<Keyboard>/enter");
            enterAction.performed += OnEnterPerformed;
            enterAction.Enable();

            // For the Keypad Enter
            keypadEnterAction = new InputAction(binding: "<Keyboard>/numpadEnter");
            keypadEnterAction.performed += OnEnterPerformed;
            keypadEnterAction.Enable();
            basisUIRaycastProcess.Initalize();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Disable the input actions when the module is disabled
            tabAction.Disable();
            enterAction.Disable();
            keypadEnterAction.Disable();

            tabAction.performed -= OnTabPerformed;
            enterAction.performed -= OnEnterPerformed;
            keypadEnterAction.performed -= OnEnterPerformed;
            basisUIRaycastProcess.OnDeInitalize();

        }
        private void OnTextInput(char character)
        {
            if (char.IsControl(character))
            {
                HandleControlCharacter(character);
            }
            else
            {
                HandleTextCharacter(character);
            }
        }

        private void HandleControlCharacter(char character)
        {
            if (character == '\b') // Backspace character
            {
                if (CurrentSelectedTMP_InputField != null)
                {
                    if (CurrentSelectedTMP_InputField.text.Length > 0)
                    {
                        CurrentSelectedTMP_InputField.text = CurrentSelectedTMP_InputField.text.Remove(CurrentSelectedTMP_InputField.text.Length - 1);
                        CurrentSelectedTMP_InputField.onValueChanged.Invoke(CurrentSelectedTMP_InputField.text);
                    }
                }
                else if (CurrentSelectedInputField != null)
                {
                    if (CurrentSelectedInputField.text.Length > 0)
                    {
                        CurrentSelectedInputField.text = CurrentSelectedInputField.text.Remove(CurrentSelectedInputField.text.Length - 1);
                        CurrentSelectedInputField.onValueChanged.Invoke(CurrentSelectedInputField.text);
                    }
                }
            }
            // Add more control character handling if needed
        }

        private void HandleTextCharacter(char character)
        {
            if (CurrentSelectedTMP_InputField != null)
            {
                CurrentSelectedTMP_InputField.text += character;
                CurrentSelectedTMP_InputField.onValueChanged.Invoke(CurrentSelectedTMP_InputField.text);
            }
            else if (CurrentSelectedInputField != null)
            {
                CurrentSelectedInputField.text += character;
                CurrentSelectedInputField.onValueChanged.Invoke(CurrentSelectedInputField.text);
            }
        }
        public override void Process()
        {
            basisUIRaycastProcess.Simulate();
            // Process your input events here
            if (EventSystem.currentSelectedGameObject != null)
            {
                var data = GetBaseEventData();
                //  ExecuteEvents.Execute(EventSystem.currentSelectedGameObject, data, ExecuteEvents.submitHandler);
                if (EventSystem.currentSelectedGameObject.TryGetComponent(out CurrentSelectedTMP_InputField))
                {
                    if (HasHoverONInput == false)
                    {
                        // Subscribe to the device change event
                        //  Keyboard.current.onTextInput += OnTextInput;
                        HasHoverONInput = true;
                        if (BasisLocalPlayer.Instance != null && BasisLocalPlayer.Instance.Move != null)
                        {
                            BasisLocalPlayer.Instance.Move.BlockMovement = true;
                        }
                        if (BasisLocalInputActions.Instance != null)
                        {
                            BasisLocalInputActions.Instance.IgnoreCrouchToggle = true;
                        }
                        if (BasisDeviceManagement.Instance.CurrentMode == "OpenVRLoader" || BasisDeviceManagement.Instance.CurrentMode == "OpenXRLoader" || ForceKeyboard)
                        {
                            BasisVirtualKeyboard.CreateMenu(CurrentSelectedInputField, CurrentSelectedTMP_InputField);
                        }
                    }
                }
                else
                {
                    if (EventSystem.currentSelectedGameObject.TryGetComponent(out CurrentSelectedInputField))
                    {
                        if (HasHoverONInput == false)
                        {
                            // Subscribe to the device change event
                            //  Keyboard.current.onTextInput += OnTextInput;
                            HasHoverONInput = true;
                            if (BasisLocalPlayer.Instance != null && BasisLocalPlayer.Instance.Move != null)
                            {
                                BasisLocalPlayer.Instance.Move.BlockMovement = true;
                            }
                            if (BasisLocalInputActions.Instance != null)
                            {
                                BasisLocalInputActions.Instance.IgnoreCrouchToggle = true;
                            }
                            if (BasisDeviceManagement.Instance.CurrentMode == "OpenVRLoader" || BasisDeviceManagement.Instance.CurrentMode == "OpenXRLoader" || ForceKeyboard)
                            {
                                BasisVirtualKeyboard.CreateMenu(CurrentSelectedInputField, CurrentSelectedTMP_InputField);
                            }
                        }
                    }
                }
            }
            else
            {
                if (HasHoverONInput)
                {
                    // Unsubscribe from the key press event
                    // Keyboard.current.onTextInput -= OnTextInput;
                    HasHoverONInput = false;
                    CurrentSelectedTMP_InputField = null;
                    CurrentSelectedInputField = null;
                    if (BasisLocalPlayer.Instance != null && BasisLocalPlayer.Instance.Move != null)
                    {
                        BasisLocalPlayer.Instance.Move.BlockMovement = false;
                    }
                    if (BasisLocalInputActions.Instance != null)
                    {
                        BasisLocalInputActions.Instance.IgnoreCrouchToggle = false;
                    }
                    var data = GetBaseEventData();
                    ExecuteEvents.Execute(EventSystem.currentSelectedGameObject, data, ExecuteEvents.submitHandler);
                }
            }
        }

        private void OnTabPerformed(InputAction.CallbackContext context)
        {
            // Handle Tab key press
            if (context.performed)
            {
                GameObject CurrentGameobject = EventSystem.currentSelectedGameObject;
                if (CurrentGameobject == null)
                {
                    return;
                }
                GameObject next = FindNextSelectable(CurrentGameobject);
                if (next != null)
                {
                    EventSystem.SetSelectedGameObject(next);
                }
            }
        }

        private void OnEnterPerformed(InputAction.CallbackContext context)
        {
            // Handle Enter key press
            if (context.performed)
            {
                GameObject current = EventSystem.currentSelectedGameObject;
                if (current != null)
                {
                    ExecuteEvents.Execute(current, new BaseEventData(EventSystem), ExecuteEvents.submitHandler);
                }
            }
        }

        private GameObject FindNextSelectable(GameObject current)
        {
            // Logic to find the next selectable UI element
            if (current.TryGetComponent<Selectable>(out Selectable Selectable))
            {
                Selectable nextSelectable = Selectable.FindSelectableOnDown();
                return nextSelectable != null ? nextSelectable.gameObject : null;
            }
            return null;
        }
    }
}