using System.IO;
using System;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace Grimoire.Utilities
{
    public static class Paths
    {
        public static string DefaultDirectory;
        public static string DefaultFileName = string.Empty;
        public static string DefaultExtension = "*";
        static string title;

        public static string Title
        {
            set
            {
                title = value;
            }
        }

        static string description;

        /// <summary>
        /// The description of the displayed dialog.
        /// </summary>
        public static string Description
        {
            set
            {
                description = value;
            }
        }

        public static bool FileMultiSelect = false;
        public static DialogResult FileResult;
        public static DialogResult SaveResult;
        public static DialogResult FolderResult;

        /// <summary>
        /// Takes user input to select a directory and return files from within the selected directory
        /// </summary>
        private static string[] filePaths
        {
            get
            {
                title = (title == null) ? "Please select desired file" : title;

                using (OpenFileDialog ofDlg = new OpenFileDialog() {
                    DefaultExt = DefaultExtension,
                    Title = title,
                    InitialDirectory = DefaultDirectory,
                    Multiselect = FileMultiSelect,
                    FileName = DefaultFileName
                }) {
                    if ((FileResult = ofDlg.ShowDialog(GUI.Main.Instance)) == DialogResult.OK)
                        return File.Exists(ofDlg.FileName) ? ofDlg.FileNames : null;
                }

                return new string[1] { null };
            }
        }

        /// <summary>
        /// Exposes a folder browser dialog to the user and returns the first element from the file paths within the selected directory
        /// </summary>
        public static string FilePath => filePaths?[0];

        /// <summary>
        /// Exposes a folder browser dialog to the user and returns an array of file paths within the selected directory
        /// </summary>
        public static string[] FilePaths => filePaths;

        public static string SavePath
        {
            get
            {
                title = (title == null) ? "Please select save location and file name" : title;

                using (SaveFileDialog svDlg = new SaveFileDialog() {
                    DefaultExt = "*",
                    Title = title,
                    InitialDirectory = DefaultDirectory,
                    FileName = DefaultFileName
                }) {
                    if ((SaveResult = svDlg.ShowDialog(GUI.Main.Instance)) == DialogResult.OK)
                        return svDlg.FileName;
                }

                return null;
            }
        }

        /// <summary>
        /// Exposes a folder browser dialog to the user and returns the users selected directory or null
        /// </summary>
        public static string FolderPath
        {
            get
            {
                using (FolderBrowserDialog fbDlg = new FolderBrowserDialog() {
                    Description = description ?? "Please select desired folder"
                }) {
                    if ((FolderResult = fbDlg.ShowDialog(GUI.Main.Instance)) == DialogResult.OK)
                        return Directory.Exists(fbDlg.SelectedPath) ? fbDlg.SelectedPath : null;
                }

                return null;
            }
        }

        /// <summary>
        /// Verify that the provided dump directories sub folders only container their respective file extensions.
        /// </summary>
        /// <param name="directory">Directory containing previously dumped client</param>
        /// <returns>True if the dump is ready to build a client</returns>
        public static Task<bool> VerifyDump(string directory, bool interactive = true, bool autoOverwrite = true)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return Task.FromResult(false);

            if (interactive)
                return Task.FromResult(VerifyDumpInternal(directory, interactive, autoOverwrite));

            return Task.Run(() => VerifyDumpInternal(directory, interactive, autoOverwrite));
        }

        static bool VerifyDumpInternal(string directory, bool interactive, bool autoOverwrite)
        {
            foreach (string extDir in Directory.GetDirectories(directory))
            {
                string dirExt = NormalizeDirectoryExtension(extDir);
                if (string.IsNullOrWhiteSpace(dirExt))
                    continue;

                foreach (string sourcePath in Directory.GetFiles(extDir))
                {
                    string name = Path.GetFileName(sourcePath);
                    string fileExt = NormalizeFileExtension(name);

                    if (string.Equals(fileExt, dirExt, StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool moveFile = !interactive || MessageBox.Show(
                        $"File: {name} does not belong to the directory: /{dirExt}/\n\nWould you like to move it?",
                        "Directory Mismatch Found",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes;

                    if (!moveFile)
                        return false;

                    string destinationDir = Path.Combine(directory, fileExt);
                    Directory.CreateDirectory(destinationDir);

                    string destinationPath = Path.Combine(destinationDir, name);
                    if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (File.Exists(destinationPath))
                    {
                        bool overwrite = !interactive && autoOverwrite;

                        if (!overwrite)
                        {
                            overwrite = MessageBox.Show(
                                "A file with the same name already exists in the destination folder.\n\nWould you like to delete it?",
                                "Duplicate File Warning",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Exclamation) == DialogResult.Yes;
                        }

                        if (!overwrite)
                            return false;

                        File.Delete(destinationPath);
                    }

                    File.Move(sourcePath, destinationPath);
                }
            }

            return true;
        }

        static string NormalizeDirectoryExtension(string directoryPath)
        {
            string name = Path.GetFileName(directoryPath)?.Trim();
            return string.IsNullOrEmpty(name) ? string.Empty : name.TrimStart('.').ToLowerInvariant();
        }

        static string NormalizeFileExtension(string filename)
        {
            string extension = Path.GetExtension(filename);
            if (!string.IsNullOrEmpty(extension))
                return extension.TrimStart('.').ToLowerInvariant();

            if (filename.Length >= 3)
                return filename.Substring(filename.Length - 3).ToLowerInvariant();

            return filename.ToLowerInvariant();
        }

        
    }
}

