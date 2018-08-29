using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using DevExpress.Persistent.Base;

namespace DevExpress.ExpressApp.Updater
{
    /// <summary>
    /// Custom code.
    /// Helper methods for app.config driven settings.
    /// </summary>
    public class UpdaterHelper
    {
        #region IsFolderIgnored

        private static ICollection<string> _ignoredFolders = null;

        private static ICollection<string> IgnoredFolders
        {
            get
            {
                if (_ignoredFolders == null)
                {
                    string excludedFolders = ConfigurationManager.AppSettings["IgnoredFolders"];
                    if (!string.IsNullOrEmpty(excludedFolders))
                    {
                        string[] excludedFoldersArray = excludedFolders.Split(new string[] { ";", "," }, StringSplitOptions.RemoveEmptyEntries);
                        _ignoredFolders = excludedFoldersArray.ToList();
                    }
                    else
                        _ignoredFolders = new List<string>();
                }
                return _ignoredFolders;
            }
        }

        /// <summary>
        /// Returns true if the folder matches the IgnoredFolders app.config setting.
        /// </summary>
        public static bool IsFolderIgnored(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return false;

            string directoryLastName = new DirectoryInfo(directoryPath).Name;
            if (string.IsNullOrEmpty(directoryLastName))
                return false;

            return IgnoredFolders.Where(f => directoryLastName.Equals(f, StringComparison.OrdinalIgnoreCase)).Any();
        }

        #endregion

        #region IsFileIgnored

        private static ICollection<string> _ignoredFilePatterns = null;

        private static ICollection<string> IgnoredFilePatterns
        {
            get
            {
                if (_ignoredFilePatterns == null)
                {
                    string excludedFiles = ConfigurationManager.AppSettings["IgnoredFilePatterns"];
                    if (!string.IsNullOrEmpty(excludedFiles))
                    {
                        string[] excludedFilesArray = excludedFiles.Split(new string[] { ";", "," }, StringSplitOptions.RemoveEmptyEntries);
                        _ignoredFilePatterns = excludedFilesArray.ToList();
                    }
                    else
                        _ignoredFilePatterns = new List<string>();
                }
                return _ignoredFilePatterns;
            }
        }

        /// <summary>
        /// Returns true if the file matches the IgnoredFilePatterns app.config setting.
        /// </summary>
        public static bool IsFileIgnored(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName))
                return false;

            foreach (string ignoredFilePattern in IgnoredFilePatterns)
            {
                if (PatternMatcher.StrictMatchPattern(ignoredFilePattern, fileName))
                    return true;
            }
            return false;
        }

        #endregion

        #region DeleteExistingFiles

        private static bool? _deleteExistingFiles = null;

        public static bool DeleteExistingFiles
        {
            get
            {
                if (_deleteExistingFiles == null)
                {
                    string deleteExistingFiles = ConfigurationManager.AppSettings["DeleteExistingFiles"];
                    if (string.Equals(deleteExistingFiles, "True", StringComparison.OrdinalIgnoreCase))
                        _deleteExistingFiles = true;
                    else
                        _deleteExistingFiles = false;
                }
                return _deleteExistingFiles.GetValueOrDefault();
            }
        }

        #endregion

        #region RemoveNotUpdatedDestinationFiles

        /// <summary>
        /// Remove the files that were not updated, but ignore the folders and files specified in app.config settings (IgnoredFolders & IgnoredFilePatterns).
        /// </summary>
        public static void RemoveNotUpdatedDestinationFiles(string destinationDirectory, List<string> updatedDestinationFiles)
        {
            //string[] destinationFiles = Directory.GetFiles(sourceDirectory, "DevExpress.*.dll|DevExpress.*.xml");
            string[] destinationFiles = Directory.GetFiles(destinationDirectory, "*.*");
            string[] additionalDestinationFiles = destinationFiles.Except(updatedDestinationFiles).ToArray();
            foreach (string additionalDestinationFilePath in additionalDestinationFiles)
            {
                if (!UpdaterHelper.IsFileIgnored(additionalDestinationFilePath))
                {
                    try
                    {
                        if (File.Exists(additionalDestinationFilePath))
                        {
                            File.Delete(additionalDestinationFilePath);
                            Tracing.Tracer.LogText("The \"{0}\" file was deleted.", additionalDestinationFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Tracing.Tracer.LogWarning("Error while deleting the \"{0}\" file: \"{1}\".", additionalDestinationFilePath, ex.Message);
                    }
                }
            }
        }

        #endregion

        #region RemoveNotUpdatedDestinationSubDirectories

        /// <summary>
        /// Remove the folders that were not updated, but ignore the ones specified in app.config settings (IgnoredFolders).
        /// </summary>
        public static void RemoveNotUpdatedDestinationSubDirectories(string[] updatedDestinationSubDirectories, string destinationDirectory)
        {
            if (string.IsNullOrEmpty(destinationDirectory) || updatedDestinationSubDirectories == null)
                return;
            string[] destinationSubDirectories = Directory.GetDirectories(destinationDirectory);
            string[] missingDestinationSubDirectories = destinationSubDirectories.Except(updatedDestinationSubDirectories).ToArray();
            foreach (string missingDestinationSubDirectory in missingDestinationSubDirectories)
            {
                if (UpdaterHelper.IsFolderIgnored(missingDestinationSubDirectory))
                    continue;

                try
                {
                    if (Directory.Exists(missingDestinationSubDirectory))
                    {
                        Directory.Delete(missingDestinationSubDirectory, true);
                        Tracing.Tracer.LogText("The \"{0}\" folder was deleted.", missingDestinationSubDirectory);
                    }
                }
                catch (Exception ex)
                {
                    Tracing.Tracer.LogWarning("Error while deleting the \"{0}\" folder: \"{1}\".", missingDestinationSubDirectory, ex.Message);
                }
            }
        }

        #endregion
    }
}
