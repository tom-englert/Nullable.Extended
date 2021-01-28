using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Ninject;
using TomsToolbox.Composition;
using TomsToolbox.Composition.Ninject;

using Task = System.Threading.Tasks.Task;

namespace Nullable.Extended.Extension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindow))]
    public sealed class ExtensionPackage : AsyncPackage
    {
        public const string PackageGuidString = "e8b6cb89-75cb-433f-a8d9-52719840e6fe";

        private readonly IKernel _kernel = new StandardKernel();
        private readonly IExportProvider _exportProvider;

        public ExtensionPackage()
        {
            _exportProvider = new ExportProvider(_kernel);
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            _kernel.BindExports(GetType().Assembly);
            _kernel.Bind<IExportProvider>().ToConstant(_exportProvider);
            _kernel.Bind<IServiceProvider>().ToConstant(this);

            await OpenToolWindowCommand.InitializeAsync(this);
        }

        public IExportProvider ExportProvider => _exportProvider;
    }
}
