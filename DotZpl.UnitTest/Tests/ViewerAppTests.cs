using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using DotZpl.Viewer.Shared;
using DotZpl.Viewer.Shared.ViewModels;

namespace DotZpl.UnitTest
{
    [TestClass]
    public class ViewerAppTests
    {
        /// <summary>
        /// Constructing the main view model loads the label corpus and renders the first label end to
        /// end (parse → analyze → build drawing) without error. Headless smoke test for the MVVM app.
        /// </summary>
        [TestMethod]
        public void MainViewModel_LoadsLabels_AndRendersFirst()
        {
            var vm = StaRunner.Run(() => NewViewModel());

            Assert.IsTrue(vm.ExampleLabels.Count > 0, "example labels loaded");
            Assert.IsTrue(vm.TestLabels.Count > 0, "test labels loaded");
            Assert.IsFalse(string.IsNullOrEmpty(vm.ZplText), "first label content loaded into the editor");
            Assert.IsFalse(string.IsNullOrEmpty(vm.PreviewZpl), "preview ZPL was applied");
            Assert.IsNull(vm.RenderError, $"first label should render without error: {vm.RenderError}");
        }

        [TestMethod]
        public void MainViewModel_SelectingLabel_AppliesSizeAndRenders()
        {
            var vm = StaRunner.Run(() =>
            {
                var v = NewViewModel();
                LabelItem label = v.TestLabels.First(l => l.Format.Contains('x'));
                v.SelectedTestLabel = label;
                return v;
            });

            Assert.IsFalse(string.IsNullOrEmpty(vm.PreviewZpl), "selecting a label renders it");
            Assert.IsTrue(vm.LabelWidth > 0 && vm.LabelHeight > 0, "size parsed from the label format");
        }

        /// <summary>Editing the ZPL text triggers a debounced live render (the preview updates on its own).</summary>
        [TestMethod]
        public void EditingZpl_TriggersLiveRender()
        {
            const string edited = "^XA^FO20,20^GB100,80,3^FS^XZ";

            string? preview = StaRunner.Run(() =>
            {
                var vm = NewViewModel();
                vm.ZplText = edited;                         // schedules a debounced render (does not apply immediately)
                Assert.AreNotEqual(edited, vm.PreviewZpl, "render is debounced, not immediate");

                PumpFor(TimeSpan.FromMilliseconds(700));     // let the ~300ms render timer fire
                return vm.PreviewZpl;
            });

            Assert.AreEqual(edited, preview, "editing the ZPL should live-update the preview");
        }

        /// <summary>Wires up the VM with WPF-flavoured test doubles: the dispatcher posts via the current
        /// STA thread's <see cref="Dispatcher"/>, and the save-file service refuses (returns null) so the
        /// VM never blocks a test on a real dialog.</summary>
        private static MainViewModel NewViewModel() => new(new TestDispatcher(), new NullFileDialogService());

        private sealed class TestDispatcher : IDispatcher
        {
            private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
            public void Post(Action action) => _dispatcher.BeginInvoke(action);
        }

        private sealed class NullFileDialogService : IFileDialogService
        {
            public Task<string?> SaveFileAsync(string title, string defaultFileName, string extension, string description)
                => Task.FromResult<string?>(null);
        }

        /// <summary>Run the dispatcher message loop for a fixed duration so DispatcherTimers can tick.</summary>
        private static void PumpFor(TimeSpan duration)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = duration };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }
}
