using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;
using Kudu.Core.Jobs;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Jobs
{
    public class BaseJobRunnerFacts
    {
        private readonly string _testJobSourceDir;
        private readonly string _testJobWorkingDir;
        private readonly Mock<IJobLogger> _mockLogger;

        public BaseJobRunnerFacts()
        {
            _testJobSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "testjobsource");
            _testJobWorkingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "testjobworking");
            _mockLogger = new Mock<IJobLogger>(MockBehavior.Strict);
        }
        
        [Fact]
        public void JobDirectoryHasChanged_NoChanges_CachedEntries_ReturnsFalse()
        {
            using (CreateTestJobDirectories())
            {
                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                Assert.Equal(9, sourceDirectoryFileMap.Count);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                Assert.Equal(9, workingDirectoryFileMap.Count);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                Assert.False(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(0, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }
        
        [Fact]
        public void JobDirectoryHasChanged_FileModifiedInSubDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                string testSubDir = Path.Combine(_testJobSourceDir, "subdir");
                File.WriteAllText(Path.Combine(testSubDir, "test2.txt"), "update");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);

                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir\\test2.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(1, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }
        
        [Fact]
        public void JobDirectoryHasChanged_MultipleFilesModifiedInSubDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                string testSubDir = Path.Combine(_testJobSourceDir, "subdir");
                File.WriteAllText(Path.Combine(testSubDir, "test2.txt"), "update");
                File.WriteAllText(Path.Combine(testSubDir, "test1.txt"), "update");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir\\test2.txt' timestamp differs between source and working directories."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir\\test1.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(2, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileModifiedInRootDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test2.txt"), "update");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(1, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_MutipleFilesModifiedInRootDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test2.txt"), "update");
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test3.txt"), "update");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' timestamp differs between source and working directories."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test3.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(2, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileAddedInRootDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test4.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test4.txt' exists in source directory but not in working directory."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(1, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_MultipleFilesAddedInRootDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test4.txt"), "test");
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test5.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test4.txt' exists in source directory but not in working directory."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test5.txt' exists in source directory but not in working directory."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(2, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileDeleted_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.Delete(Path.Combine(_testJobSourceDir, "test2.txt"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' has been deleted."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(0, copyToWorkingDirectory.Count);
                Assert.Equal(1, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_MultipleFilesDeleted_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.Delete(Path.Combine(_testJobSourceDir, "test2.txt"));
                File.Delete(Path.Combine(_testJobSourceDir, "test1.txt"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' has been deleted."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test1.txt' has been deleted."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(0, copyToWorkingDirectory.Count);
                Assert.Equal(2, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileAddedInWorkingDir_ReturnsFalse()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobWorkingDir, "test4.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                Assert.False(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(0, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileModifiedInWorkingDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobWorkingDir, "test2.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(1, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_MultipleFilesModifiedInWorkingDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobWorkingDir, "test1.txt"), "test");
                File.WriteAllText(Path.Combine(_testJobWorkingDir, "test3.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test1.txt' timestamp differs between source and working directories."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test3.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(2, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FileDeletedInWorkingDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.Delete(Path.Combine(_testJobWorkingDir, "test2.txt"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' exists in source directory but not in working directory."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(1, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_MultipleFilesDeletedInWorkingDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.Delete(Path.Combine(_testJobWorkingDir, "test2.txt"));
                File.Delete(Path.Combine(_testJobWorkingDir, "test3.txt"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' exists in source directory but not in working directory."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test3.txt' exists in source directory but not in working directory."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(2, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_IsCaseInsensitive()
        {
            using (CreateTestJobDirectories())
            {
                // create a case mismatch
                File.Move(Path.Combine(_testJobWorkingDir, "test1.txt"), Path.Combine(_testJobWorkingDir, "TEST1.TXT"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = sourceDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                Assert.False(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(0, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_FilesCreatedModifiedAndDeleted_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test1.txt"), "test");
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test4.txt"), "test");
                File.Delete(Path.Combine(_testJobSourceDir, "test2.txt"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test2.txt' has been deleted."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test4.txt' exists in source directory but not in working directory."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'test1.txt' timestamp differs between source and working directories."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(2, copyToWorkingDirectory.Count);
                Assert.Equal(1, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_NewSubDirCreated_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                // make a new sub directory
                string testSubDir = Path.Combine(_testJobSourceDir, "subdir2");
                Directory.CreateDirectory(testSubDir);

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job subdirectory 'subdir2' exists in source directory but not in working directory."));
                
                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(1, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_NewFileCreatedInNewSubDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                // add some files in a sub directory
                string testSubDir = Path.Combine(_testJobSourceDir, "subdir2");
                Directory.CreateDirectory(testSubDir);
                File.WriteAllText(Path.Combine(testSubDir, "test1.txt"), "test");

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job subdirectory 'subdir2' exists in source directory but not in working directory."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir2\\test1.txt' exists in source directory but not in working directory."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(2, copyToWorkingDirectory.Count);
                Assert.Equal(0, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_DeleteFilesInSubDirLeavingEmptySubDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                File.Delete(Path.Combine(_testJobSourceDir, "subdir\\test2.txt"));

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir\\test2.txt' has been deleted."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(0, copyToWorkingDirectory.Count);
                Assert.Equal(1, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void JobDirectoryHasChanged_DeleteEntireSubDir_ReturnsTrue()
        {
            using (CreateTestJobDirectories())
            {
                Directory.Delete(Path.Combine(_testJobSourceDir, "subdir"), true);

                var sourceDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobSourceDir);
                var workingDirectoryFileMap = BaseJobRunner.GetJobDirectoryFileAndSubDirMap(_testJobWorkingDir);
                var cachedDirectoryFileMap = workingDirectoryFileMap;
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job subdirectory 'subdir' has been deleted."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir\\test1.txt' has been deleted."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir\\test2.txt' has been deleted."));
                _mockLogger.Setup(p => p.LogInformation("Job directory change detected: Job file 'subdir\\test3.txt' has been deleted."));

                Assert.True(BaseJobRunner.JobDirectoryHasChangedFileDiffConveyedInSets(sourceDirectoryFileMap, workingDirectoryFileMap, cachedDirectoryFileMap, copyToWorkingDirectory, removeFromWorkingDirectory, _mockLogger.Object));
                Assert.Equal(0, copyToWorkingDirectory.Count);
                Assert.Equal(4, removeFromWorkingDirectory.Count);

                _mockLogger.VerifyAll();
            }
        }

        [Fact]
        public void UpdateAppConfigs_DoesNotModifyLastWriteTime()
        {
            using (CreateTestJobDirectories())
            {
                FileInfo fileInfo = new FileInfo(Path.Combine(_testJobWorkingDir, "job.exe.config"));
                DateTime before = fileInfo.LastWriteTimeUtc;

                SettingsProcessor.Instance.AppSettings.Add("test", "test");

                Mock<IAnalytics> mockAnalytics = new Mock<IAnalytics>();
                BaseJobRunner.UpdateAppConfigs(_testJobWorkingDir, mockAnalytics.Object);

                fileInfo.Refresh();
                DateTime after = fileInfo.LastWriteTimeUtc;
                Assert.Equal(before, after);

                Configuration config = ConfigurationManager.OpenExeConfiguration(Path.Combine(_testJobWorkingDir, "job.exe"));
                Assert.Equal("test", config.AppSettings.Settings["test"].Value);
            }
        }

        private DisposableAction CreateTestJobDirectories()
        {
            var cleanupAction = new Action(() =>
            {
                if (Directory.Exists(_testJobSourceDir))
                {
                    Directory.Delete(_testJobSourceDir, true);
                }
                if (Directory.Exists(_testJobWorkingDir))
                {
                    Directory.Delete(_testJobWorkingDir, true);
                }
            });

            Directory.CreateDirectory(_testJobSourceDir);

            // add some files in root
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test1.txt"), "test");
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test2.txt"), "test");
            File.WriteAllText(Path.Combine(_testJobSourceDir, "test3.txt"), "test");

            File.WriteAllText(Path.Combine(_testJobSourceDir, "job.exe"), "binary");
            File.WriteAllText(Path.Combine(_testJobSourceDir, "job.exe.config"), "<configuration></configuration>");

            // add some files in a sub directory
            string testSubDir = Path.Combine(_testJobSourceDir, "subdir");
            Directory.CreateDirectory(testSubDir);
            File.WriteAllText(Path.Combine(testSubDir, "test1.txt"), "test");
            File.WriteAllText(Path.Combine(testSubDir, "test2.txt"), "test");
            File.WriteAllText(Path.Combine(testSubDir, "test3.txt"), "test");

            // now, copy all the files to the working directory
            if (Directory.Exists(_testJobWorkingDir))
            {
                Directory.Delete(_testJobWorkingDir, true);
            }
            FileSystemHelpers.CopyDirectoryRecursive(_testJobSourceDir, _testJobWorkingDir);

            return new DisposableAction(cleanupAction);
        }
    }
}
