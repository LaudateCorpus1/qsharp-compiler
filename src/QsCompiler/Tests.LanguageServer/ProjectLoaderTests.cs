﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.CompilationBuilder;
using Microsoft.Quantum.QsCompiler.ReservedKeywords;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Quantum.QsLanguageServer.Testing
{
    public partial class BasicFunctionality
    {
        private static string ProjectFileName(string project) =>
            Path.Combine("TestProjects", project, $"{project}.csproj");

        internal static Uri ProjectUri(string project) =>
            new(Path.GetFullPath(ProjectFileName(project)));

        internal async Task<(Uri, ProjectInformation?)> GetProjectInformationAsync(string project)
        {
            var uri = ProjectUri(project);
            var projDir = Path.GetDirectoryName(uri.AbsolutePath) ?? "";

            var initParams = TestUtils.GetInitializeParams();
            initParams.RootUri = new Uri(projDir);
            await this.rpc.NotifyWithParameterObjectAsync(Methods.Initialize.Name, initParams);

            var projectInfo = await this.GetProjectInformationAsync(uri);
            if (projectInfo is null)
            {
                return (uri, null);
            }

            var stringReader = new StringReader(projectInfo);
            var reader = System.Xml.XmlReader.Create(stringReader);

            void ReadElementGroup(string groupName, out List<string> paths)
            {
                paths = new List<string>();
                if (!reader.IsStartElement(groupName))
                {
                    reader.ReadToNextSibling(groupName);
                }

                reader.ReadStartElement(groupName);
                while (reader.IsStartElement("File"))
                {
                    paths.Add(reader.GetAttribute("Path") ?? "");
                    reader.ReadToNextSibling("File");
                }
            }

            var outputPath = reader.IsStartElement("ProjectInfo")
                ? reader.GetAttribute("OutputPath")
                : null;
            var targetCapability = reader.GetAttribute("TargetCapability");
            var processorArch = reader.GetAttribute("ProcessorArchitecture");

            reader.ReadStartElement("ProjectInfo");
            ReadElementGroup("Sources", out var sources);
            ReadElementGroup("ProjectReferences", out var projectRefs);
            ReadElementGroup("References", out var references);
            reader.ReadEndElement();

            var projectProperties = new Dictionary<string, string?>
            {
                { MSBuildProperties.TargetPath, outputPath },
                { MSBuildProperties.ResolvedTargetCapability, targetCapability },
                { MSBuildProperties.ResolvedProcessorArchitecture, processorArch },
            };

            var infos = new ProjectInformation(
                sourceFiles: sources,
                projectReferences: projectRefs,
                references: references,
                projectProperties);
            return (uri, infos);
        }

        [TestMethod]
        public void GetGlobalProperties()
        {
            var expectedFramework = "Some-framework";
            var result = ProjectLoader.GlobalProperties(expectedFramework);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("false", result["BuildProjectReferences"]);
            Assert.AreEqual("false", result["EnableFrameworkPathOverride"]);
            Assert.AreEqual(expectedFramework, result["TargetFramework"]);
        }

        [TestMethod]
        public void SupportedTargetFrameworks()
        {
            var loader = new ProjectLoader();
            Assert.IsTrue(loader.IsSupportedQsFramework("netstandard2.0"));
            Assert.IsTrue(loader.IsSupportedQsFramework("netstandard2.1"));
            Assert.IsTrue(loader.IsSupportedQsFramework("netcoreapp3.1"));
            Assert.IsTrue(loader.IsSupportedQsFramework("net6.0"));
        }

        [TestMethod]
        public void FindProjectTargetFramework()
        {
            static void CompareFramework(string project, string? expected)
            {
                var projectFileName = ProjectFileName(project);
                var props = new ProjectLoader().DesignTimeBuildProperties(projectFileName, (x, y) => (y.Contains('.') ? 1 : 0) - (x.Contains('.') ? 1 : 0));
                if (!props.TryGetValue("TargetFramework", out var actual))
                {
                    actual = null;
                }

                Assert.AreEqual(expected, actual);
            }

            var testProjects = new (string, string?)[]
            {
                ("test1", "netcoreapp3.1"),
                ("test2", "netstandard2.0"),
                ("test3", "netstandard2.0"),
                ("test3", "netstandard2.0"),
                ("test4", "netcoreapp3.1"),
                ("test5", "netcoreapp3.1"),
                ("test6", "netstandard2.0"),
                ("test7", "net461"),
                ("test8", null),
                ("test9", "netcoreapp3.1"),
                ("test10", "netcoreapp3.1"),
                ("test11", "netcoreapp3.1"),
                ("test12", "netstandard2.1"),
                ("test13", "net6.0"),
            };

            foreach (var (project, framework) in testProjects)
            {
                CompareFramework(project, framework);
            }
        }

        [TestMethod]
        public async Task LoadNonQSharpProjectsAsync()
        {
            var invalidProjects = new string[]
            {
                "test1",
                "test2",
                "test8",
            };

            foreach (var project in invalidProjects)
            {
                var (_, context) = await this.GetProjectInformationAsync(project);
                Assert.IsNull(context);
            }
        }

        [TestMethod]
        public async Task LoadUnsupportedQSharpProjectAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test9");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test9.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "Operation9.qs"),
            };

            Assert.IsTrue(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());
            Assert.IsFalse(context.UsesXunitHelper());

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadUnsupportedQSharpCoreLibrariesAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test3");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test3.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "Operation3a.qs"),
                Path.Combine(projDir, "Operation3b.qs"),
                Path.Combine(projDir, "sub1", "Operation3b.qs"),
                Path.Combine(projDir, "sub1", "sub2", "Operation3a.qs"),
            };

            Assert.IsTrue(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());
            Assert.IsFalse(context.UsesXunitHelper());

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadQSharpCoreLibrariesAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test12");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test12.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "format", "Unformatted.qs"),
                Path.Combine(projDir, "Operation12a.qs"),
                Path.Combine(projDir, "Operation12b.qs"),
                Path.Combine(projDir, "sub1", "Operation12b.qs"),
                Path.Combine(projDir, "sub1", "sub2", "Operation12a.qs"),
            };

            Assert.IsTrue(context.UsesQSharpCore());
            Assert.IsFalse(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());
            Assert.IsFalse(context.UsesXunitHelper());

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadQSharpFrameworkLibraryAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test7");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test7.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "Operation.qs"),
            };

            Assert.IsTrue(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());
            Assert.IsFalse(context.UsesXunitHelper());

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadUnsupportedQSharpConsoleAppAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test4");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test4.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "Operation4.qs"),
            };

            Assert.IsTrue(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());
            Assert.IsFalse(context.UsesXunitHelper());
            Assert.IsTrue(context.UsesProject("test3.csproj"));

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadOutdatedQSharpConsoleAppAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test10");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test10.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "Operation10.qs"),
            };

            Assert.IsTrue(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadQSharpConsoleAppAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test11");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test11.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "Operation11.qs"),
            };

            Assert.IsTrue(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadTargetedQSharpExecutableAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test17");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test17.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "MeasureBell.qs"),
            };

            Assert.AreEqual(TargetCapabilityModule.FromName("AdaptiveExecution"), context.Properties.TargetCapability);
            Assert.AreEqual(AssemblyConstants.QCIProcessor, context.Properties.ProcessorArchitecture);

            Assert.IsTrue(context.UsesQSharpCore());
            Assert.IsFalse(context.UsesIntrinsics());
            Assert.IsFalse(context.UsesDll("Microsoft.Quantum.Type3.Core.dll"));
            Assert.IsTrue(context.UsesCanon());
            Assert.IsFalse(context.UsesXunitHelper());

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadQSharpUnitTestAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test5");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test5.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                // Compilation target set to none for "Operation5.qs",
                Path.Combine(projDir, "Tests5.qs"),
                Path.Combine(projDir, "test.folder", "Operation5.qs"),
            };

            Assert.IsTrue(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());
            Assert.IsTrue(context.UsesXunitHelper());
            Assert.IsTrue(context.UsesProject("test3.csproj"));
            Assert.IsTrue(context.UsesProject("test4.csproj"));

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }

        [TestMethod]
        public async Task LoadQSharpMultiFrameworkLibraryAsync()
        {
            var (projectFile, context) = await this.GetProjectInformationAsync("test6");
            var projDir = Path.GetDirectoryName(projectFile.AbsolutePath) ?? "";
            Assert.IsNotNull(context);
            Assert.AreEqual("test6.dll", Path.GetFileName(context!.Properties.DllOutputPath));
            Assert.IsTrue((Path.GetDirectoryName(context.Properties.DllOutputPath) ?? "").StartsWith(projDir));

            var qsFiles = new string[]
            {
                Path.Combine(projDir, "..", "test7", "Operation.qs"), // linked file
                Path.Combine(projDir, "Operation6a.qs"),
                Path.Combine(projDir, "sub1", "Operation6a.qs"),
            };

            Assert.IsTrue(context.UsesIntrinsics());
            Assert.IsTrue(context.UsesCanon());
            Assert.IsFalse(context.UsesXunitHelper());
            Assert.IsTrue(context.UsesProject("test3.csproj"));

            var expected = qsFiles.Select(Path.GetFullPath).Select(p => new Uri(p).AbsolutePath).ToArray();
            CollectionAssert.AreEquivalent(expected, context.SourceFiles.ToArray());
        }
    }

    internal static class CompilationContext
    {
        private static void LogOutput(string msg, MessageType level) =>
            Console.WriteLine($"[{level}]: {msg}");

        internal static EditorState Editor =>
            new(new ProjectLoader(LogOutput), null, null, null, null);

        internal static bool UsesDll(this ProjectInformation info, string dll) => info.References.Any(r => r.EndsWith(dll));

        internal static bool UsesProject(this ProjectInformation info, string projectFileName) => info.ProjectReferences.Any(r => r.EndsWith(projectFileName));

        // NB: We check whether the project uses either the 0.3–0.5 name (Primitives) or the 0.6– name (Intrinsic).
        internal static bool UsesIntrinsics(this ProjectInformation info) => info.UsesDll("Microsoft.Quantum.Intrinsic.dll") || info.UsesDll("Microsoft.Quantum.Primitives.dll");

        internal static bool UsesQSharpCore(this ProjectInformation info) => info.UsesDll("Microsoft.Quantum.QSharp.Core.dll");

        internal static bool UsesCanon(this ProjectInformation info) =>
            info.UsesDll("Microsoft.Quantum.Canon.dll") ||
            info.UsesDll("Microsoft.Quantum.Standard.dll");

        internal static bool UsesXunitHelper(this ProjectInformation info) => info.UsesDll("Microsoft.Quantum.Simulation.XUnit.dll");
    }
}
