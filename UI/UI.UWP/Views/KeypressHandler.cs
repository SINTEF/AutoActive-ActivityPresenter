using System.Collections.Generic;
using Windows.System;
using Windows.UI.Xaml.Input;
using SINTEF.AutoActive.UI.Pages;
using Xamarin.Forms.Platform.UWP;
using KeyEventArgs = SINTEF.AutoActive.UI.Pages.KeyEventArgs;

namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class KeypressHandler
    {
        // Which key modifiers (Shift/Ctrl) are pressed.
        // Static fields are used as key modifiers should be shared through the application
        private static KeyModifiers _keyModifiers;

        public static void KeyPageKeyDown(KeypressPage keyPage, KeyEventArgs key)
        {
            if (ShouldIgnoreEvent()) return;

            keyPage.InvokeKeyDown(key);
        }

        public static void KeyPageKeyUp(KeypressPage keyPage, KeyEventArgs key)
        {
            if (ShouldIgnoreEvent()) return;

            keyPage.InvokeKeyUp(key);
        }

        public static bool ShouldIgnoreEvent()
        {
            var element = FocusManager.GetFocusedElement();
            var elementType = element.GetType();

            var ignoredTypes = new List<System.Type> { typeof(FormsTextBox) };

            return ignoredTypes.Contains(elementType);
        }

        private static KeyModifiers ClearKeyModifiers(KeyModifiers key)
        {
            if ((_keyModifiers & key) == key)
            {
                return _keyModifiers & ~key;
            }
            return _keyModifiers;

        }
        private static KeyModifiers SetKeyModifiers(KeyModifiers key)
        {
            if ((_keyModifiers & key) == key)
            {
                return _keyModifiers;
            }
            return _keyModifiers | key;
        }

        private static KeyModifiers VirtualKeyToModifier(VirtualKey key)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (key)
            {
                case VirtualKey.Shift:
                case VirtualKey.LeftShift:
                case VirtualKey.RightShift:
                    return KeyModifiers.Shift;
                case VirtualKey.Control:
                case VirtualKey.LeftControl:
                case VirtualKey.RightControl:
                    return KeyModifiers.Ctrl;
                default:
                    return KeyModifiers.None;
            }
        }

        public static KeyEventArgs VirtualKeyToKeyEvent(VirtualKey key, bool handled, bool isDown)
        {
            var currentKey = VirtualKeyToModifier(key);
            if (currentKey != KeyModifiers.None)
            {
                _keyModifiers = isDown ? SetKeyModifiers(currentKey) : ClearKeyModifiers(currentKey);
            }

            return new KeyEventArgs { Key = key.ToString(), Handled = handled, Modifiers = _keyModifiers };
        }
    }
}
