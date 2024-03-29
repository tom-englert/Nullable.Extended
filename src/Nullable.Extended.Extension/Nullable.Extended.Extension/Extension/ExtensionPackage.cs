﻿using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;

using Ninject;

using Nullable.Extended.Extension.Views;

using TomsToolbox.Composition;
using TomsToolbox.Composition.Ninject;

using Task = System.Threading.Tasks.Task;

namespace Nullable.Extended.Extension.Extension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [InstalledProductRegistration(@"Nullable Extended Extension", @"Tools to keep your nullability annotations lean and mean.", "Nullable.Extended")]
    [ProvideMenuResource("Menus.ctmenu", 1)] 
    [ProvideToolWindow(typeof(NullForgivingToolWindow))]
    public sealed class ExtensionPackage : AsyncPackage
    {
        public const string PackageGuidString = "e8b6cb89-75cb-433f-a8d9-52719840e6fe";

        private readonly IKernel _kernel = new StandardKernel();

        public ExtensionPackage()
        {
            ExportProvider = new ExportProvider(_kernel);
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            _kernel.BindExports(GetType().Assembly);
            _kernel.Bind<IExportProvider>().ToConstant(ExportProvider);
            _kernel.Bind<IServiceProvider>().ToConstant(this);

            await OpenToolWindowCommand.InitializeAsync(this);
        }

        public IExportProvider ExportProvider { get; }
    }
}
