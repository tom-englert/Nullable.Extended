using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;

using Nullable.Extended.AnalyzerFramework;
using Nullable.Extended.Extension.Views;

using TomsToolbox.Composition;
using TomsToolbox.Composition.MicrosoftExtensions;

using ServiceProvider = Microsoft.Extensions.DependencyInjection.ServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Nullable.Extended.Extension.Extension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [InstalledProductRegistration("Nullable Extended Extension", "Tools to keep your nullability annotations lean and mean.", "Nullable.Extended")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(NullForgivingToolWindow))]
    public sealed class ExtensionPackage : AsyncPackage
    {
        public const string PackageGuidString = "e8b6cb89-75cb-433f-a8d9-52719840e6fe";

        private ServiceProvider? _serviceProvider;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.BindExports(GetType().Assembly, typeof(IAnalyzerEngine).Assembly);
            serviceCollection.AddSingleton<IExportProvider>(serviceProvider => new ExportProviderAdapter(serviceProvider));

            _serviceProvider = serviceCollection.BuildServiceProvider();
            ExportProvider = _serviceProvider.GetRequiredService<IExportProvider>();

            await OpenToolWindowCommand.InitializeAsync(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _serviceProvider?.Dispose();
            }

            base.Dispose(disposing);
        }

        public IExportProvider ExportProvider { get; private set; } = null!;
    }
}
