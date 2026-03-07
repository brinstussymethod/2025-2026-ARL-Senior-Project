using System.IO;
using Moq;
using UnBox3D.Utils;
using Xunit;

namespace UnBox3D.Tests.Utils
{
    /// <summary>
    /// Tests the functionalities of FileSystem in this order: 
    /// 1. Directory 
    /// 2. File 
    /// 3. CombinePaths 
    /// </summary>
    public class FileSystemTests
    {
        // TODO: relocate these paths to their test cases.
        static readonly string sampleDirectoryPath = Path.GetFullPath("../../../"); // NOTE: Contains root of testing code outside of bin/(Debug | Release)
        private IFileSystem _sampleFileSystem;

        public FileSystemTests()
        {
            _sampleFileSystem = new FileSystem();
        }

        [Fact]
        public void FileSystem_ShouldNotFindDirectory_WhenNotPresent()
        {
            /// Arrange
            var sampleDudSubDirectoryPath2 = Path.Combine(sampleDirectoryPath, "Foo");

            /// Action
            var absentDirectoryResult = _sampleFileSystem.DoesDirectoryExists(sampleDudSubDirectoryPath2);

            /// Assert
            Assert.False(absentDirectoryResult);
        }

        [Fact]
        public void FileSystem_ShouldFindDirectory_WhenPresent()
        {
            /// Arrange
            var sampleActualDirectoryPath = Path.Combine(sampleDirectoryPath, "Models");

            /// Action
            var presentDirectoryResult = _sampleFileSystem.DoesDirectoryExists(sampleActualDirectoryPath);

            /// Assert
             Assert.True(presentDirectoryResult);
        }

        [Fact]
        public void FileSystem_ShouldCreateDirectory_WithPath()
        {
            /// Arrange
            var sampleDudSubdirectoryPath = Path.Combine(sampleDirectoryPath, "Extra");
            Assert.False(_sampleFileSystem.DoesDirectoryExists(sampleDudSubdirectoryPath));

            /// Action
            _sampleFileSystem.CreateDirectory(sampleDudSubdirectoryPath);

            /// Assert
            Assert.True(_sampleFileSystem.DoesDirectoryExists(sampleDudSubdirectoryPath));
        }

        [Fact]
        public void FileSystem_ShouldDeleteDirectory_WithPath()
        {
            /// Arrange
            var sampleDudSubdirectoryPath = Path.Combine(sampleDirectoryPath, "Extra");
            _sampleFileSystem.CreateDirectory(sampleDudSubdirectoryPath);

            /// Action
            _sampleFileSystem.DeleteDirectory(sampleDudSubdirectoryPath, false);

            /// Assert
            Assert.False(_sampleFileSystem.DoesDirectoryExists(sampleDudSubdirectoryPath));
        }

        [Fact]
        public void FileSystem_ShouldNotFindFile_WhenNotPresent()
        {
            /// Arrange
            var sampleFakeFilePath = Path.Combine(sampleDirectoryPath, "Foo", "Bar.cs");

            /// Action
            var hasFileResult = _sampleFileSystem.DoesFileExists(sampleFakeFilePath);

            /// Assert
            Assert.False(hasFileResult);
        }

        [Fact]
        public void FileSystem_ShouldFindFile_WhenPresent()
        {
            /// Arrange
            var sampleDemoFilePath = Path.Combine(sampleDirectoryPath, "Models", "CommandHistoryTests.cs");

            /// Action
            var hasFileResult = _sampleFileSystem.DoesFileExists(sampleDemoFilePath);

            /// Assert
            Assert.True(hasFileResult);
        }

        [Fact]
        public void FileSystem_ShouldWriteToFile_WhenPresent()
        {
            /// Arrange
            var sampleDemoFilePath = Path.Combine(sampleDirectoryPath, "Baz0a.txt");

            /// Action
            _sampleFileSystem.WriteToFile(sampleDemoFilePath, "Hello World!");

            /// Assert
            var sampleFileCreatedResult = _sampleFileSystem.DoesFileExists(sampleDemoFilePath);
            Assert.True(sampleFileCreatedResult);
        }

        [Fact]
        public void FileSystem_ShouldReadFile_WhenPresent()
        {
            /// Arrange
            var sampleDemoFilePath = Path.Combine(sampleDirectoryPath, "Baz0b.txt");
            _sampleFileSystem.WriteToFile(sampleDemoFilePath, "Hello World!");

            /// Action
            var sampleContent = _sampleFileSystem.ReadFile(sampleDemoFilePath);

            /// Assert
            Assert.Equal("Hello World!", sampleContent);
        }

        [Fact]
        public void FileSystem_ShouldAppendToFile_WhenGiven()
        {
            /// Arrange
            var sampleDemoFilePath = Path.Combine(sampleDirectoryPath, "Baz0c.txt");
            _sampleFileSystem.WriteToFile(sampleDemoFilePath, "Good");

            /// Action
            _sampleFileSystem.AppendToFile(sampleDemoFilePath, "bye!");

            /// Assert
            var finalSampleContent = _sampleFileSystem.ReadFile(sampleDemoFilePath);
            Assert.Contains("Goodbye!", finalSampleContent);
        }

        [Fact]
        public void FileSystem_ShouldDeleteFile_WhenPresent()
        {
            /// Arrange
            var sampleDemoFilePath2 = Path.Combine(sampleDirectoryPath, "Baz2.txt");
            _sampleFileSystem.WriteToFile(sampleDemoFilePath2, "This should be missing");

            /// Action
            _sampleFileSystem.DeleteFile(sampleDemoFilePath2);

            /// Assert
            Assert.False(_sampleFileSystem.DoesFileExists(sampleDemoFilePath2));
        }

        [Fact]
        public void FileSystem_ShouldGetFileSize_WhenPresent()
        {
            /// Arrange
            var sampleDemoFilePath3 = Path.Combine(sampleDirectoryPath, "Baz3.txt");
            _sampleFileSystem.WriteToFile(sampleDemoFilePath3, "Hello World!");

            /// Action
            var sampleDemoSize = _sampleFileSystem.GetFileSize(sampleDemoFilePath3);

            /// Assert
            Assert.Equal(12, sampleDemoSize);
        }

        /// TODO add more tests for MoveFile method...
        [Fact]
        public void FileSystem_ShouldRenameFile_WhenSelfMove()
        {
            /// Arrange
            var sampleDemoFilePath4 = Path.Combine(sampleDirectoryPath, "Baz4.txt");
            _sampleFileSystem.WriteToFile(sampleDemoFilePath4, "Testing!");
            var sampleNewFilePath4 = Path.Combine(sampleDirectoryPath, "Waldo.txt");

            /// Action
            _sampleFileSystem.MoveFile(sampleDemoFilePath4, sampleNewFilePath4);

            /// Assert
            Assert.True(_sampleFileSystem.DoesFileExists(sampleNewFilePath4) && !_sampleFileSystem.DoesFileExists(sampleDemoFilePath4));
        }

        [Fact]
        public void FileSystem_ShouldGiveDudFilePath_WhenJoinDudDirectoryAndFile()
        {
            /// Arrange
            var expectedDudFilePath = Path.Combine(sampleDirectoryPath, "Foo", "Bar.cs");
            string[] testDudFilePathElements = { sampleDirectoryPath, "Foo", "Bar.cs" };

            /// Action
            var tempDudFilePath = _sampleFileSystem.CombinePaths(testDudFilePathElements);

            /// Assert
            Assert.Equal(tempDudFilePath, expectedDudFilePath);
        }

        [Fact]
        public void FileSystem_ShouldGiveRealFilePath_WhenJoinRealDirectoryAndFile()
        {
            /// Arrange
            var expectedRealFilePath = Path.Combine(sampleDirectoryPath, "Models", "CommandHistoryTests.cs");
            string[] tempElements = { sampleDirectoryPath, "Models", "CommandHistoryTests.cs" };

            /// Action
            var tempFilePath = _sampleFileSystem.CombinePaths(tempElements);

            /// Assert
            Assert.Equal(tempFilePath, expectedRealFilePath);
        }
    }
}
