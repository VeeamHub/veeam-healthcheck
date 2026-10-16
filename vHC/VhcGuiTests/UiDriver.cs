using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using VeeamHealthCheck;
using Xunit;

namespace VhcGuiTests
{
    public static class UiDriver
    {
        /// <summary>Real mouse down/up at the control's centre, in the window's coordinates.</summary>
        public static void Click(Window w, Control c)
        {
            var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
            w.MouseDown(p, MouseButton.Left);
            w.MouseUp(p, MouseButton.Left);
        }

        /// <summary>Replaces the TextBox contents with <paramref name="text"/> via key text input.</summary>
        public static void Type(Window w, TextBox box, string text)
        {
            box.Focus();
            box.SelectAll();
            w.KeyTextInput(text);
        }

        /// <summary>
        /// Pumps the dispatcher until <paramref name="cond"/> holds (about 2s), then asserts it.
        /// Needed because handlers such as termsCheckBox_Checked and SetUiAsync continue on Task.Run.
        /// </summary>
        public static async Task Until(Func<bool> cond)
        {
            for (int i = 0; i < 200 && !cond(); i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.True(cond());
        }

        /// <summary>Waits for SetUiAsync to finish; scrubBox being checked is its last step.</summary>
        public static Task WaitForStartup(VhcGui w) =>
            Until(() => w.FindControl<CheckBox>("scrubBox").IsChecked == true);

        public static T FindRequired<T>(Window w, string name) where T : Control
        {
            var c = w.FindControl<T>(name);
            Assert.NotNull(c);
            return c;
        }

        /// <summary>The single owned window of type T, after letting pending UI work run.</summary>
        public static T Owned<T>(Window owner) where T : Window
        {
            Dispatcher.UIThread.RunJobs();
            return owner.OwnedWindows.OfType<T>().Single();
        }
    }
}
