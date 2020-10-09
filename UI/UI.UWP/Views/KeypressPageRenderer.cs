using System.Collections.Generic;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Input;
using SINTEF.AutoActive.UI.Pages;
using SINTEF.AutoActive.UI.UWP.Views;
using Xamarin.Forms.Platform.UWP;
using KeyEventArgs = SINTEF.AutoActive.UI.Pages.KeyEventArgs;

[assembly: ExportRenderer(typeof(KeypressPage), typeof(KeypressPageRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class KeypressPageRenderer : PageRenderer
    {
        // Which key modifiers (Shift/Ctrl) are pressed.
        // Static fields are used as key modifiers should be shared through the application
        private static KeyModifiers _keyModifiers;

#if false
        public KeypressPageRenderer()
        {
            Loaded += (sender, e) =>
            {
                _keyModifiers = KeyModifiers.None;
                var window = CoreWindow.GetForCurrentThread();
                if (window == null) return;
                window.KeyDown += ControlOnKeyDown;
                window.KeyUp += Control_KeyUp;
            };
            Unloaded += (sender, e) =>
            {
                var window = CoreWindow.GetForCurrentThread();
                if (window == null) return;
                window.KeyDown -= ControlOnKeyDown;
                window.KeyUp -= Control_KeyUp;
                _keyModifiers = KeyModifiers.None;
            };
        }

        private void Control_KeyUp(CoreWindow coreWindow, Windows.UI.Core.KeyEventArgs args)
        {
            if (!(Element is KeypressPage page))
            {
                return;
            }
            var keyArgs = VirtualKeyToKeyEvent(args.VirtualKey, args.Handled, false);
            KeyPageKeyUp(page, keyArgs);
        }

        private void ControlOnKeyDown(CoreWindow coreWindow, Windows.UI.Core.KeyEventArgs args)
        {
            if (!(Element is KeypressPage page))
            {
                return;
            }
            var keyArgs = VirtualKeyToKeyEvent(args.VirtualKey, args.Handled, true);
            KeyPageKeyDown(page, keyArgs);
        }
#endif

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
