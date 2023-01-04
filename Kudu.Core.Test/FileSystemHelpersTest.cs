using Kudu.Core.Infrastructure;
using Moq;
using System;
using System.IO.Abstractions;
using Xunit;
using Kudu.TestHarness;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Kudu.Core.Test
{
    public class FileSystemHelpersTest
    {
        private readonly string _testJobSourceDir;
        private readonly string _testJobWorkingDir;

        public FileSystemHelpersTest()
        {
            _testJobSourceDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "testjobsource");
            _testJobWorkingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "testjobworking");
        }

        [Fact]
        public void EnsureDirectoryCreatesDirectoryIfNotExists()
        {
            var fileSystem = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            directory.Setup(m => m.Exists("foo")).Returns(false);
            FileSystemHelpers.Instance = fileSystem.Object;

            string path = FileSystemHelpers.EnsureDirectory("foo");

            Assert.Equal("foo", path);
            directory.Verify(m => m.CreateDirectory("foo"), Times.Once());
        }

        [Fact]
        public void EnsureDirectoryDoesNotCreateDirectoryIfNotExists()
        {
            var fileSystem = new Mock<IFileSystem>();
            var directory = new Mock<DirectoryBase>();
            fileSystem.Setup(m => m.Directory).Returns(directory.Object);
            directory.Setup(m => m.Exists("foo")).Returns(true);
            FileSystemHelpers.Instance = fileSystem.Object;

            string path = FileSystemHelpers.EnsureDirectory("foo");

            Assert.Equal("foo", path);
            directory.Verify(m => m.CreateDirectory("foo"), Times.Never());
        }

        [Theory]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foo", true)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foo\bar", true)]
        [InlineData(@"x:\temp\bar", @"x:\temp\Foo", false)]
        [InlineData(@"x:\temp\Foo\bar", @"x:\temp\foo", false)]
        [InlineData(@"x:\temp\foo\", @"x:\temp\Foo\", true)]
        [InlineData(@"x:\temp\Foo\", @"x:\temp\foo", true)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foo\", true)]
        [InlineData(@"x:\temp\Foo", @"x:\temp\foobar", false)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foobar\", false)]
        [InlineData(@"x:\temp\foo\..", @"x:\temp\Foo", true)]
        [InlineData(@"x:\temp\..\temp\foo\..", @"x:\temp\Foo", true)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foo\..", false)]
        // slashes
        [InlineData(@"x:/temp\foo", @"x:\temp\Foo", true)]
        [InlineData(@"x:\temp/foo", @"x:\temp\Foo\bar", true)]
        [InlineData(@"x:\temp\bar", @"x:/temp\Foo", false)]
        [InlineData(@"x:\temp\Foo\bar", @"x:\temp/foo", false)]
        [InlineData(@"x:/temp\foo\", @"x:\temp\Foo\", true)]
        [InlineData(@"x:\temp/Foo\", @"x:\temp\foo", true)]
        [InlineData(@"x:\temp\foo", @"x:\temp/Foo\", true)]
        [InlineData(@"x:\temp/Foo", @"x:\temp\foobar", false)]
        [InlineData(@"x:\temp\foo", @"x:\temp\Foobar/", false)]
        [InlineData(@"x:\temp\foo/..", @"x:/temp\Foo", true)]
        [InlineData(@"x:\temp\..\temp/foo\..", @"x:/temp\Foo", true)]
        [InlineData(@"x:\temp/foo", @"x:\temp\Foo\..", false)]
        public void IsSubfolderOfTests(string parent, string child, bool expected)
        {
            Assert.Equal(expected, FileSystemHelpers.IsSubfolder(parent, child));
        }

        [Fact]
        public void IsFileSystemReadOnlyBasicTest()
        {
            // In non-azure env, read-only is false
            Assert.Equal(false, FileSystemHelpers.IsFileSystemReadOnly());

            // mock Azure Env
            using (KuduUtils.MockAzureEnvironment())
            {
                // able to create and delete folder, should return false
                var fileSystem = new Mock<IFileSystem>();
                var dirBase = new Mock<DirectoryBase>();
                var dirInfoBase = new Mock<DirectoryInfoBase>();
                var dirInfoFactory = new Mock<IDirectoryInfoFactory>();

                fileSystem.Setup(f => f.Directory).Returns(dirBase.Object);
                fileSystem.Setup(f => f.DirectoryInfo).Returns(dirInfoFactory.Object);

                dirBase.Setup(d => d.CreateDirectory(It.IsAny<string>())).Returns(dirInfoBase.Object);
                dirInfoFactory.Setup(d => d.FromDirectoryName(It.IsAny<string>())).Returns(dirInfoBase.Object);

                FileSystemHelpers.Instance = fileSystem.Object;
                FileSystemHelpers.TmpFolder = @"D:\";   // value doesn`t really matter, just need to have something other than default value

                Assert.Equal(false, FileSystemHelpers.IsFileSystemReadOnly());

                // Read-Only should return true
                dirBase.Setup(d => d.CreateDirectory(It.IsAny<string>())).Throws<UnauthorizedAccessException>();
                Assert.Equal(true, FileSystemHelpers.IsFileSystemReadOnly());
            }
        }

        [Fact]
        public void Ensure_CopyDirectoryFromFileSystemDiff_NoChange()
        {
            using (CreateTestJobDirectories())
            {
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();
                
                FileSystemHelpers.CopyDirectoryFromFileSystemDiff(_testJobSourceDir, _testJobWorkingDir, copyToWorkingDirectory, removeFromWorkingDirectory);
                
                // get list of file system entries, strip the leading temp directory info, and sort both lists since GetFileSystemEntries doesn't
                // guarantee order of resulting list
                string[] jobSourceDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobSourceDir);
                string[] jobWorkingDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobWorkingDir);

                IEnumerable<string> jobSourceDirFileSysStripped = stripTempPath(jobSourceDirFileSys).OrderBy(s => s);
                IEnumerable<string> jobWorkingDirFileSysStripped = stripTempPath(jobWorkingDirFileSys).OrderBy(s => s);

                Assert.Equal(jobSourceDirFileSysStripped, jobWorkingDirFileSysStripped);
            }
        }

        [Fact]
        public void Ensure_CopyDirectoryFromFileSystemDiff_NewFileToCopy()
        {
            using (CreateTestJobDirectories())
            {
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                File.WriteAllText(Path.Combine(_testJobSourceDir, "test4.txt"), "adding a new file to source");

                FileInfo test4 = new FileInfo(Path.Combine(_testJobSourceDir, "test4.txt"));
                copyToWorkingDirectory.Add(test4);

                FileSystemHelpers.CopyDirectoryFromFileSystemDiff(_testJobSourceDir, _testJobWorkingDir, copyToWorkingDirectory, removeFromWorkingDirectory);

                // get list of file system entries, strip the leading temp directory info, and sort both lists since GetFileSystemEntries doesn't
                // guarantee order of resulting list
                string[] jobSourceDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobSourceDir);
                string[] jobWorkingDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobWorkingDir);

                IEnumerable<string> jobSourceDirFileSysStripped = stripTempPath(jobSourceDirFileSys).OrderBy(s => s);
                IEnumerable<string> jobWorkingDirFileSysStripped = stripTempPath(jobWorkingDirFileSys).OrderBy(s => s);

                Assert.Equal(jobSourceDirFileSysStripped, jobWorkingDirFileSysStripped);
            }
        }

        [Fact]
        public void Ensure_CopyDirectoryFromFileSystemDiff_NewDirToCopy()
        {
            using (CreateTestJobDirectories())
            {
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                string testSubDir = Path.Combine(_testJobSourceDir, "subdir3");
                Directory.CreateDirectory(testSubDir);

                DirectoryInfo subdir3 = new DirectoryInfo(Path.Combine(_testJobSourceDir, "subdir3"));
                copyToWorkingDirectory.Add(subdir3);

                FileSystemHelpers.CopyDirectoryFromFileSystemDiff(_testJobSourceDir, _testJobWorkingDir, copyToWorkingDirectory, removeFromWorkingDirectory);

                // get list of file system entries, strip the leading temp directory info, and sort both lists since GetFileSystemEntries doesn't
                // guarantee order of resulting list
                string[] jobSourceDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobSourceDir);
                string[] jobWorkingDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobWorkingDir);

                IEnumerable<string> jobSourceDirFileSysStripped = stripTempPath(jobSourceDirFileSys).OrderBy(s => s);
                IEnumerable<string> jobWorkingDirFileSysStripped = stripTempPath(jobWorkingDirFileSys).OrderBy(s => s);

                Assert.Equal(jobSourceDirFileSysStripped, jobWorkingDirFileSysStripped);
            }
        }

        [Fact]
        public void Ensure_CopyDirectoryFromFileSystemDiff_FileToRemove()
        {
            using (CreateTestJobDirectories())
            {
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                FileInfo test1 = new FileInfo(Path.Combine(_testJobSourceDir, "test1.txt"));
                File.Delete(Path.Combine(_testJobSourceDir, "test1.txt"));

                removeFromWorkingDirectory.Add(test1);

                FileSystemHelpers.CopyDirectoryFromFileSystemDiff(_testJobSourceDir, _testJobWorkingDir, copyToWorkingDirectory, removeFromWorkingDirectory);

                // get list of file system entries, strip the leading temp directory info, and sort both lists since GetFileSystemEntries doesn't
                // guarantee order of resulting list
                string[] jobSourceDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobSourceDir);
                string[] jobWorkingDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobWorkingDir);

                IEnumerable<string> jobSourceDirFileSysStripped = stripTempPath(jobSourceDirFileSys).OrderBy(s => s);
                IEnumerable<string> jobWorkingDirFileSysStripped = stripTempPath(jobWorkingDirFileSys).OrderBy(s => s);

                Assert.Equal(jobSourceDirFileSysStripped, jobWorkingDirFileSysStripped);
            }
        }

        [Fact]
        public void Ensure_CopyDirectoryFromFileSystemDiff_DirToRemove()
        {
            using (CreateTestJobDirectories())
            {
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                string testSubDir = Path.Combine(_testJobSourceDir, "subdir2");
                DirectoryInfo subdir2 = new DirectoryInfo(testSubDir);
                Directory.Delete(testSubDir);

                removeFromWorkingDirectory.Add(subdir2);

                FileSystemHelpers.CopyDirectoryFromFileSystemDiff(_testJobSourceDir, _testJobWorkingDir, copyToWorkingDirectory, removeFromWorkingDirectory);

                // get list of file system entries, strip the leading temp directory info, and sort both lists since GetFileSystemEntries doesn't
                // guarantee order of resulting list
                string[] jobSourceDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobSourceDir);
                string[] jobWorkingDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobWorkingDir);

                IEnumerable<string> jobSourceDirFileSysStripped = stripTempPath(jobSourceDirFileSys).OrderBy(s => s);
                IEnumerable<string> jobWorkingDirFileSysStripped = stripTempPath(jobWorkingDirFileSys).OrderBy(s => s);

                Assert.Equal(jobSourceDirFileSysStripped, jobWorkingDirFileSysStripped);
            }
        }

        [Fact]
        public void Ensure_CopyDirectoryFromFileSystemDiff_FileAndDirToCopy()
        {
            using (CreateTestJobDirectories())
            {
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                string testSubDir3 = Path.Combine(_testJobSourceDir, "subdir3");
                Directory.CreateDirectory(testSubDir3);
                File.WriteAllText(Path.Combine(testSubDir3, "test1.txt"), "test");

                DirectoryInfo subdir3 = new DirectoryInfo(testSubDir3);
                FileInfo file1 = new FileInfo(Path.Combine(testSubDir3, "test1.txt"));
                copyToWorkingDirectory.Add(subdir3);
                copyToWorkingDirectory.Add(file1);

                FileSystemHelpers.CopyDirectoryFromFileSystemDiff(_testJobSourceDir, _testJobWorkingDir, copyToWorkingDirectory, removeFromWorkingDirectory);

                // get list of file system entries, strip the leading temp directory info, and sort both lists since GetFileSystemEntries doesn't
                // guarantee order of resulting list
                string[] jobSourceDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobSourceDir);
                string[] jobWorkingDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobWorkingDir);

                IEnumerable<string> jobSourceDirFileSysStripped = stripTempPath(jobSourceDirFileSys).OrderBy(s => s);
                IEnumerable<string> jobWorkingDirFileSysStripped = stripTempPath(jobWorkingDirFileSys).OrderBy(s => s);

                Assert.Equal(jobSourceDirFileSysStripped, jobWorkingDirFileSysStripped);
            }
        }

        [Fact]
        public void Ensure_CopyDirectoryFromFileSystemDiff_FileAndDirToRemove()
        {
            using (CreateTestJobDirectories())
            {
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                string testSubDir = Path.Combine(_testJobSourceDir, "subdir");

                DirectoryInfo subdir = new DirectoryInfo(testSubDir);
                FileInfo file1 = new FileInfo(Path.Combine(testSubDir, "test1.txt"));
                FileInfo file2 = new FileInfo(Path.Combine(testSubDir, "test2.txt"));
                FileInfo file3 = new FileInfo(Path.Combine(testSubDir, "test3.txt"));
                removeFromWorkingDirectory.Add(subdir);
                removeFromWorkingDirectory.Add(file1);
                removeFromWorkingDirectory.Add(file2);
                removeFromWorkingDirectory.Add(file3);

                Directory.Delete(testSubDir, recursive: true);

                FileSystemHelpers.CopyDirectoryFromFileSystemDiff(_testJobSourceDir, _testJobWorkingDir, copyToWorkingDirectory, removeFromWorkingDirectory);

                // get list of file system entries, strip the leading temp directory info, and sort both lists since GetFileSystemEntries doesn't
                // guarantee order of resulting list
                string[] jobSourceDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobSourceDir);
                string[] jobWorkingDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobWorkingDir);

                IEnumerable<string> jobSourceDirFileSysStripped = stripTempPath(jobSourceDirFileSys).OrderBy(s => s);
                IEnumerable<string> jobWorkingDirFileSysStripped = stripTempPath(jobWorkingDirFileSys).OrderBy(s => s);

                Assert.Equal(jobSourceDirFileSysStripped, jobWorkingDirFileSysStripped);
            }
        }

        [Fact]
        public void Ensure_CopyDirectoryFromFileSystemDiff_FilesAndDirsToAddAndRemove()
        {
            using (CreateTestJobDirectories())
            {
                HashSet<FileSystemInfo> copyToWorkingDirectory = new HashSet<FileSystemInfo>(), removeFromWorkingDirectory = new HashSet<FileSystemInfo>();

                // add file to the root of the source directory
                File.WriteAllText(Path.Combine(_testJobSourceDir, "test4.txt"), "test");
                FileInfo test4 = new FileInfo(Path.Combine(_testJobSourceDir, "test4.txt"));
                copyToWorkingDirectory.Add(test4);

                // delete the subdir and all files in it
                string testSubDir = Path.Combine(_testJobSourceDir, "subdir");

                DirectoryInfo subdir = new DirectoryInfo(testSubDir);
                FileInfo file1 = new FileInfo(Path.Combine(testSubDir, "test1.txt"));
                FileInfo file2 = new FileInfo(Path.Combine(testSubDir, "test2.txt"));
                FileInfo file3 = new FileInfo(Path.Combine(testSubDir, "test3.txt"));
                removeFromWorkingDirectory.Add(subdir);
                removeFromWorkingDirectory.Add(file1);
                removeFromWorkingDirectory.Add(file2);
                removeFromWorkingDirectory.Add(file3);

                Directory.Delete(testSubDir, recursive: true);

                // create new subdir3 and add test1.txt within this subdir
                string testSubDir3 = Path.Combine(_testJobSourceDir, "subdir3");

                Directory.CreateDirectory(testSubDir3);
                File.WriteAllText(Path.Combine(testSubDir3, "test1.txt"), "test");

                DirectoryInfo subdir3 = new DirectoryInfo(testSubDir3);
                FileInfo subdir3file1 = new FileInfo(Path.Combine(testSubDir3, "test1.txt"));
                copyToWorkingDirectory.Add(subdir3file1);
                copyToWorkingDirectory.Add(subdir3);

                FileSystemHelpers.CopyDirectoryFromFileSystemDiff(_testJobSourceDir, _testJobWorkingDir, copyToWorkingDirectory, removeFromWorkingDirectory);

                // get list of file system entries, strip the leading temp directory info, and sort both lists since GetFileSystemEntries doesn't
                // guarantee order of resulting list
                string[] jobSourceDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobSourceDir);
                string[] jobWorkingDirFileSys = FileSystemHelpers.GetFileSystemEntries(_testJobWorkingDir);

                IEnumerable<string> jobSourceDirFileSysStripped = stripTempPath(jobSourceDirFileSys).OrderBy(s => s);
                IEnumerable<string> jobWorkingDirFileSysStripped = stripTempPath(jobWorkingDirFileSys).OrderBy(s => s);

                Assert.Equal(jobSourceDirFileSysStripped, jobWorkingDirFileSysStripped);
            }
        }

        // auxiliary method to strip the temporary path from a string
        // e.g. Path.GetTempPath()\Path.GetRandomFileName()\testjobsource\dir1\myfile.txt
        // turns into just dir1\myfile.txt
        private IEnumerable<string> stripTempPath(string[] paths)
        {
            List<string> result = new List<string>();
            Array.ForEach(paths, path => {
                string trimmedPath = "";

                // trim off beginning _testJobSourceDir or _testJobWorkingDir
                if (path.StartsWith(_testJobSourceDir))
                {
                    trimmedPath = path.Substring(_testJobSourceDir.Length + 1);
                }
                else
                {
                    trimmedPath = path.Substring(_testJobWorkingDir.Length + 1);
                }

                result.Add(trimmedPath);
            });
            return result;
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

            // add an empty directory
            string testSubDir2 = Path.Combine(_testJobSourceDir, "subdir2");
            Directory.CreateDirectory(testSubDir2);

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
