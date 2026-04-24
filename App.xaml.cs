using System.Windows;
using System.Reflection;

namespace Ojaswat
{
    public partial class App : Application
    {
        // ══════════════════════════════════════════════════════════════════════
        // WHY A STATIC LOCATOR?
        //
        // When a ContentControl hosts a string ("dash"), its DataContext IS
        // that string — not the MainViewModel. DataTemplate children inherit
        // the string as DataContext, breaking {Binding StatTotal} etc.
        //
        // Passing VM via constructor (original approach) is clean but requires
        // the old Page(vm) constructor pattern. With DataTemplates WPF calls
        // the parameterless constructor, so constructor injection is impossible.
        //
        // Solution: store the one shared VM on App. Every page reads App.VM.
        // This is the standard Service Locator pattern for WPF shell scenarios.
        // ══════════════════════════════════════════════════════════════════════
        public static ViewModels.MainViewModel VM { get; private set; } = null!;

        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        internal static void SetVM(ViewModels.MainViewModel vm) => VM = vm;
    }
}
