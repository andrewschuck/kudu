﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Settings;
using Kudu.Core.SiteExtensions;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Infrastructure
{
    public class PathWindowsUtility : PathUtilityBase
    {
        private const string ProgramFiles64bitKey = "ProgramW6432";

        /// <summary>
        /// The version of node.exe that would be on PATH, when the user does not specify/specifies invalid node versions.
        /// </summary>
        private const string DefaultNodeVersion = "0.10.28";

        /// <summary>
        /// Maps to the version of NPM that shipped with the DefaultNodeVersion
        /// </summary>
        private const string DefaultNpmVersion = "1.4.9";

        // this api is used to add git path to %path% and pick git.exe to be used for GitExecutable
        internal override string ResolveGitPath()
        {
            // as of git 2.8.1, git.exe exists in multiple locations.
            // we explicitly prefer one under Git\cmd.
            string gitPath = ResolveGitInstallDirPath();
            return Path.Combine(gitPath, "cmd", "git.exe");
        }

        internal override string[] ResolveGitToolPaths()
        {
            // as of git 2.8.1, various unix tools are installed in multiple paths.
            // add them to %path%.
            // As of git 2.14.1 curl no longer exists in usr/bin. Use the one from mingw32/bin (mingw64/bin) instead
            // We add both mingw32 and mingw64, but it will only end up adding those that actually exist to the PATH
            string gitPath = ResolveGitInstallDirPath();
            return new[]
            {
                Path.Combine(gitPath, "bin"),
                Path.Combine(gitPath, "usr", "bin"),
                Path.Combine(gitPath, "mingw32", "bin"),
                Path.Combine(gitPath, "mingw64", "bin")
            };
        }

        internal override string ResolveHgPath()
        {
            string relativePath = Path.Combine("Mercurial", "hg.exe");
            return ResolveRelativePathToProgramFiles(relativePath, relativePath, Resources.Error_FailedToLocateHg);
        }

        internal override string ResolveSSHPath()
        {
            // version that before 2.5, ssh.exe has different path than in version 2.5
            string gitPath = ResolveGitInstallDirPath();
            string path = Path.Combine(gitPath, "bin", "ssh.exe");
            if (File.Exists(path))
            {
                return path;
            }

            // as of git 2.8.1, ssh.exe is under Git\usr\bin folder
            return Path.Combine(gitPath, "usr", "bin", "ssh.exe");
        }

        internal override string ResolveBashPath()
        {
            // as of git 2.8.1, bash.exe is under Git\bin folder
            string gitPath = ResolveGitInstallDirPath();
            return Path.Combine(gitPath, "bin", "bash.exe");
        }

        public override string ResolveLocalSitePath()
        {
            return System.Environment.ExpandEnvironmentVariables("%SystemDrive%\\local");
        }

        internal override string ResolveNpmJsPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string npmCliPath = Path.Combine("node_modules", "npm", "bin", "npm-cli.js");
            string npmVersion = ResolveNpmVersion();

            // 1. Attempt to look for the file under the S24 updated path that looks like
            // "C:\Program Files (x86)\npm\1.3.8\node_modules\npm\bin\npm-cli.js"
            string npmPath = Path.Combine(programFiles, "npm", npmVersion, npmCliPath);
            if (File.Exists(npmPath))
            {
                return npmPath;
            }

            // 2. Attempt to look for the file under the pre-S24 npm path
            // "C:\Program Files (x86)\npm\1.3.8\bin\npm-cli.js"
            npmPath = Path.Combine(programFiles, "npm", npmVersion, "bin", "npm-cli.js");
            if (File.Exists(npmPath))
            {
                return npmPath;
            }

            // 3. Use the default npm path from the NodeJS installation
            // "C:\Program Files (x86)\nodejs\node_modules\npm\bin\npm-cli.js"
            return Path.Combine(programFiles, "nodejs", npmCliPath);
        }

        internal override string ResolveMSBuild15Dir()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            List<string> probPaths = new List<string>
            {
                Path.Combine(programFiles, "Microsoft Visual Studio", "2017", "Enterprise", "MSBuild", "15.0", "Bin"), // visual studio Enterprise
                Path.Combine(programFiles, "Microsoft Visual Studio", "2017", "Professional", "MSBuild", "15.0", "Bin"), // visual studio Professional
                Path.Combine(programFiles, "Microsoft Visual Studio", "2017", "Community", "MSBuild", "15.0", "Bin"), // visual studio Community
                Path.Combine(programFiles, "Microsoft Visual Studio", "2017", "BuildTools", "MSBuild", "15.0", "Bin") // msbuild tools
                // above is for public kudu, below is for azure
            };
            probPaths.Add(Path.Combine(programFiles, "MSBuild-15.9.21.664", "MSBuild", "MSBuild", "15.0", "Bin"));

            return probPaths.FirstOrDefault(path => Directory.Exists(path));
        }

        internal override string ResolveMSBuild16Dir(string targetFramework)
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            List<string> probPaths = new List<string>
            {
                Path.Combine(programFiles, "Microsoft Visual Studio", "2019", "Enterprise",   "MSBuild", "Current", "Bin"), // visual studio Enterprise
                Path.Combine(programFiles, "Microsoft Visual Studio", "2019", "Professional", "MSBuild", "Current", "Bin"), // visual studio Professional
                Path.Combine(programFiles, "Microsoft Visual Studio", "2019", "Community",    "MSBuild", "Current", "Bin"), // visual studio Community
                Path.Combine(programFiles, "Microsoft Visual Studio", "2019", "BuildTools",   "MSBuild", "Current", "Bin"), // msbuild tools
                // above is for public kudu, below is for azure
            };

            if (VsHelper.IsDotNet31Version(targetFramework) && ScmHostingConfigurations.UseMSBuild167ForDotnet31)
            {
                probPaths.Add(Path.Combine(programFiles, "MSBuilds", "16.7.0", "MSBuild", "Current", "Bin"));
            }
            else if (VsHelper.IsDotNet7Version(targetFramework))
            {
                // Using ResolveMSBuild1670Dir as it's picking the latest MSBuild version.
                probPaths.Add(ResolveLatestMSBuildDir());
            }

            probPaths.Add(Path.Combine(programFiles, "MSBuild-16.4", "MSBuild", "Current", "Bin"));

            return probPaths.FirstOrDefault(path => Directory.Exists(path));
        }

        internal override string ResolveLatestMSBuildDir()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            List<string> probPaths = new List<string>
            {
                Path.Combine(programFiles, "Microsoft Visual Studio", "2019", "Enterprise",   "MSBuild", "Current", "Bin"), // visual studio Enterprise
                Path.Combine(programFiles, "Microsoft Visual Studio", "2019", "Professional", "MSBuild", "Current", "Bin"), // visual studio Professional
                Path.Combine(programFiles, "Microsoft Visual Studio", "2019", "Community",    "MSBuild", "Current", "Bin"), // visual studio Community
                Path.Combine(programFiles, "Microsoft Visual Studio", "2019", "BuildTools",   "MSBuild", "Current", "Bin"), // msbuild tools
                // above is for public kudu, below is for azure
            };

            if (FileSystemHelpers.DirectoryExists(Path.Combine(programFiles, "MSBuilds")))
            {
                // Iterate through MSbuild versions inside Program Files (x86)\MSBuilds and fetch the latest one
                string latestMsBuildStr = null;
                SemanticVersion latestMsBuild = null;
                foreach (string msBuild in FileSystemHelpers.GetDirectories(Path.Combine(programFiles, "MSBuilds")))
                {
                    var versionStr = Path.GetFileName(msBuild);
                    if (SemanticVersion.TryParse(versionStr, out SemanticVersion currVersion))
                    {
                        if (latestMsBuild == null || currVersion.CompareTo(latestMsBuild) > 0)
                        {
                            latestMsBuildStr = versionStr;
                            latestMsBuild = currVersion;
                        }
                    }
                }

                var pinnedVersion = VsHelper.MSBuildVersion;
                var pinnedPath = string.IsNullOrEmpty(pinnedVersion) ? null : Path.Combine(programFiles, "MSBuilds", pinnedVersion, "MSBuild", "Current", "Bin");
                if (FileSystemHelpers.DirectoryExists(pinnedPath))
                {
                    probPaths.Add(pinnedPath);
                }
                else if (!string.IsNullOrEmpty(latestMsBuildStr))
                {
                    string latestMsBuildPath = Path.Combine(programFiles, "MSBuilds", latestMsBuildStr, "MSBuild", "Current", "Bin");
                    if (FileSystemHelpers.DirectoryExists(latestMsBuildPath))
                    {
                        probPaths.Add(latestMsBuildPath);
                    }
                }
            }

            // TODO: Remove this after ANT93
            // Fallback to 16.8.0 if present
            probPaths.Add(Path.Combine(programFiles, "MSBuild-16.8.0", "MSBuild", "Current", "Bin"));

            return probPaths.FirstOrDefault(path => FileSystemHelpers.DirectoryExists(path));
        }

        internal override string ResolveMSBuildPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "MSBuild", "14.0", "Bin", "MSBuild.exe");
        }

        internal override string ResolveVsTestPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "Microsoft Visual Studio 11.0", "Common7", "IDE", "CommonExtensions", "Microsoft", "TestWindow", "vstest.console.exe");
        }

        internal override string ResolveSQLCmdPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "Microsoft SQL Server", "110", "Tools", "Binn", "sqlcmd.exe");
        }

        internal override string ResolveNpmGlobalPrefix()
        {
            string appDataDirectory = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDirectory, "npm");
        }

        internal override string ResolveVCTargetsPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            //This path has to end with \ for the environment variable to work
            return Path.Combine(programFiles, "MSBuild", "Microsoft.Cpp", "v4.0", @"V140\");
        }

        internal override string ResolveVCInstallDirPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            //This path has to end with \ for the environment variable to work
            return Path.Combine(programFiles, "Microsoft Visual Studio 14.0", @"VC\");
        }

        internal static string ResolveGitInstallDirPath()
        {
            // look up whether x86 or x64 of git was installed.
            // if both exists, x64 will be used (assuming it is newly installed).
            string programFiles = SystemEnvironment.GetEnvironmentVariable(ProgramFiles64bitKey) ?? SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFiles);
            string path = Path.Combine(programFiles, "Git");
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "cmd", "git.exe")))
            {
                return path;
            }

            programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            path = Path.Combine(programFiles, "Git");
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "cmd", "git.exe")))
            {
                return path;
            }

            throw new InvalidOperationException(Resources.Error_FailedToLocateGit);
        }

        private static string ResolveNodeVersion()
        {
            bool fromAppSetting;
            return ResolveNodeVersion(out fromAppSetting);
        }

        private static string ResolveNodeVersion(out bool fromAppSetting)
        {
            // We can't use ConfigurationManager.AppSettings[] here because during git push this runs in kudu.exe context
            // which doesn't have access to Azure ConfigurationManager.AppSettings[] of the site
            string appSettingNodeVersion = SystemEnvironment.GetEnvironmentVariable("APPSETTING_WEBSITE_NODE_DEFAULT_VERSION");

            if (IsNodeVersionInstalled(appSettingNodeVersion))
            {
                fromAppSetting = true;
                return appSettingNodeVersion;
            }
            else
            {
                fromAppSetting = false;
                return DefaultNodeVersion;
            }
        }

        private static string ResolveNpmVersion()
        {
            return ResolveNpmVersion(ResolveNodeVersion());
        }

        private static string ResolveNpmVersion(string nodeVersion)
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string appSettingNpmVersion = SystemEnvironment.GetEnvironmentVariable("WEBSITE_NPM_DEFAULT_VERSION");

            if (!string.IsNullOrEmpty(appSettingNpmVersion))
            {
                return appSettingNpmVersion;
            }
            else if (nodeVersion.Equals("4.1.2", StringComparison.OrdinalIgnoreCase))
            {
                // This case is only to work around node version 4.1.2 with npm 2.x failing to publish ASP.NET 5 apps due to long path issues.
                return "3.3.6";
            }
            else
            {
                string npmTxtPath = Path.Combine(programFiles, "nodejs", nodeVersion, "npm.txt");

                return FileSystemHelpers.FileExists(npmTxtPath) ? FileSystemHelpers.ReadAllTextFromFile(npmTxtPath).Trim() : DefaultNpmVersion;
            }
        }

        private static bool IsNodeVersionInstalled(string version)
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return (!String.IsNullOrEmpty(version)) &&
                   FileSystemHelpers.FileExists(Path.Combine(programFiles, "nodejs", version, "node.exe"));
        }

        internal override List<string> ResolveNodeNpmPaths()
        {
            var paths = new List<string>();
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            bool fromAppSetting;
            string nodeVersion = ResolveNodeVersion(out fromAppSetting);

            // Only set node.exe path when WEBSITE_NODE_DEFAULT_VERSION is not set in app setting and the path is invalid.
            if (!fromAppSetting)
            {
                string nodePath = Path.Combine(programFiles, "nodejs", nodeVersion);
                if (FileSystemHelpers.FileExists(Path.Combine(nodePath, "node.exe")))
                {
                    paths.Add(nodePath);
                }
            }

            // Only set npm.cmd path when npm.cmd can be found
            string npmVersion = ResolveNpmVersion(nodeVersion);
            string npmPath = Path.Combine(programFiles, "npm", npmVersion);
            if (FileSystemHelpers.FileExists(Path.Combine(npmPath, "npm.cmd")))
            {
                paths.Add(npmPath);
            }

            return paths;
        }

        public override bool PathsEquals(string path1, string path2)
        {
            if (path1 == null)
            {
                return path2 == null;
            }

            return String.Equals(CleanPath(path1), CleanPath(path2), StringComparison.OrdinalIgnoreCase);
        }

        internal override string ResolveFSharpCPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, @"Microsoft SDKs", "F#", "3.1", "Framework", "v4.0", "Fsc.exe");
        }

        internal override string ResolveNpmToolsPath(string toolName)
        {
            // If there is a TOOLNAME_PATH specified, then use that.
            // Otherwise use the pre-installed one
            var toolPath = SystemEnvironment.GetEnvironmentVariable(String.Format("APPSETTING_{0}_PATH", toolName));
            if (String.IsNullOrEmpty(toolPath))
            {
                var programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
                var toolRootPath = Path.Combine(programFiles, toolName);
                if (Directory.Exists(toolRootPath))
                {
                    // If there is a TOOLNAME_VERSION defined, use that.
                    // Otherwise use the latest one.
                    var userVersion = SystemEnvironment.GetEnvironmentVariable(String.Format("APPSETTING_{0}_VERSION", toolName));

                    toolPath = String.IsNullOrEmpty(userVersion)
                        ? Directory.GetDirectories(toolRootPath).OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
                        : Path.Combine(toolRootPath, userVersion);
                }
            }

            return String.IsNullOrEmpty(toolPath)
                ? String.Empty
                : Path.Combine(toolPath, String.Format("{0}.cmd", toolName));
        }

        private static string ResolveRelativePathToProgramFiles(string relativeX86Path, string relativeX64Path, string errorMessage)
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, relativeX86Path);
            if (!File.Exists(path))
            {
                programFiles = SystemEnvironment.GetEnvironmentVariable(ProgramFiles64bitKey) ?? SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFiles);
                path = Path.Combine(programFiles, relativeX64Path);
            }

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(errorMessage);
            }

            return path;
        }
    }
}
