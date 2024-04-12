using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WindowsGSM.Functions
{
    public static class ZipHelper
    {
        public static void CreateFromDirectory(
            string sourceDirectoryName
            , string destinationArchiveFileName
            , CompressionLevel compressionLevel
            , Predicate<string> filter
        )
        {
            CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, compressionLevel, false, Encoding.UTF8,
                filter);
        }

        public static void CreateFromDirectory(
            string sourceDirectoryName
            , string destinationArchiveFileName
            , CompressionLevel compressionLevel
            , bool includeBaseDirectory
            , Encoding entryNameEncoding
            , Predicate<string> filter
        )
        {
            if (string.IsNullOrEmpty(sourceDirectoryName))
            {
                throw new ArgumentNullException(nameof(sourceDirectoryName));
            }

            if (string.IsNullOrEmpty(destinationArchiveFileName))
            {
                throw new ArgumentNullException(nameof(destinationArchiveFileName));
            }

            var filesToAdd = Directory.GetFiles(sourceDirectoryName, "*", SearchOption.AllDirectories);
            var entryNames = GetEntryNames(filesToAdd, sourceDirectoryName, includeBaseDirectory);
            using (var zipFileStream = new FileStream(destinationArchiveFileName, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
                {
                    for (var i = 0; i < filesToAdd.Length; i++)
                    {
                        if (!filter(filesToAdd[i]))
                        {
                            continue;
                        }

                        archive.CreateEntryFromFile(filesToAdd[i], entryNames[i], compressionLevel);
                    }
                }
            }
        }

        private static string[] GetEntryNames(string[] names, string sourceFolder, bool includeBaseName)
        {
            if (names == null || names.Length == 0)
                return Array.Empty<string>();

            if (includeBaseName)
                sourceFolder = Path.GetDirectoryName(sourceFolder);

            var length = string.IsNullOrEmpty(sourceFolder) ? 0 : sourceFolder.Length;
            if (length > 0 && sourceFolder != null && sourceFolder[length - 1] != Path.DirectorySeparatorChar &&
                sourceFolder[length - 1] != Path.AltDirectorySeparatorChar)
                length++;

            var result = new string[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                result[i] = names[i].Substring(length);
            }

            return result;
        }
    }
}