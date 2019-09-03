using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;

using NuGet.Commands;
using NuGet.Common;

namespace Link.Command
{
    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    public class ProjectWrapper : MSBuildUser
    {
        // Its type is Microsoft.Build.Evaluation.Project
        private dynamic _project;

        private ILogger _logger;

        public NuGet.Configuration.IMachineWideSettings MachineWideSettings { get; set; }

        public static ProjectWrapper ProjectCreator(PackArgs packArgs, string path)
        {
            return new ProjectWrapper(packArgs.MsBuildDirectory.Value, path, packArgs.Properties)
            {
                LogLevel = packArgs.LogLevel,
                Logger = packArgs.Logger,
                MachineWideSettings = packArgs.MachineWideSettings,
            };
        }

        public ProjectWrapper(string msbuildDirectory, string path, IDictionary<string, string> projectProperties)
        {
            ProjectPath = Path.GetDirectoryName(path);
            LoadAssemblies(msbuildDirectory);

            // Create project, allowing for assembly load failures
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolve);

            try
            {
                var project = Activator.CreateInstance(
                    _projectType,
                    path,
                    projectProperties,
                    null);
                Initialize(project);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(AssemblyResolve);
            }
        }

        public ProjectWrapper(string msbuildDirectory, dynamic project)
        {
            LoadAssemblies(msbuildDirectory);
            Initialize(project);
        }

        private ProjectWrapper(
            string msbuildDirectory,
            Assembly msbuildAssembly,
            Assembly frameworkAssembly)
        {
            _msbuildDirectory = msbuildDirectory;
            _msbuildAssembly = msbuildAssembly;
            _frameworkAssembly = frameworkAssembly;
            LoadTypes();
        }

        private void Initialize(dynamic project)
        {
            _project = project;

            // Get the target framework of the project
            string targetFrameworkMoniker = _project.GetPropertyValue("TargetFrameworkMoniker");
            if (!string.IsNullOrEmpty(targetFrameworkMoniker))
            {
                TargetFramework = new FrameworkName(targetFrameworkMoniker);
            }

            var outputPath = _project.GetPropertyValue("OutputPath");
            if (!string.IsNullOrEmpty(outputPath))
            {
                OutputPath = Path.Combine(ProjectPath, outputPath);
            }
        }

        public FrameworkName TargetFramework
        {
            get;
            private set;
        }

        public string ProjectPath
        {
            get;
        }

        public string OutputPath
        {
            get;
            private set;
        }

        public LogLevel LogLevel { get; set; }

        public ILogger Logger
        {
            get
            {
                return _logger ?? NullLogger.Instance;
            }
            set
            {
                _logger = value;
            }
        }
    }
}
