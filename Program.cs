using System.IO.Compression;

namespace RapidMinerDataPreProcessor
{
    internal class Program
    {
        public struct Options
        {
            public bool DeleteNonCsvFiles { get; set; }
            public bool MoveFiles { get; set; }
            public string Directory { get; set; }
            public string OutputDirectory { get; set; }

            public static readonly string Extension = "dat";
        }

        private static void DeleteEmptyDirectories(string? path)
        {
            string?[] directories = Directory.GetDirectories(path);
            foreach (var directory in directories)
            {
                DeleteEmptyDirectories(directory);
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                    Directory.Delete(directory, false);
            }
        }

        private static List<string> FindFilesInNestedDirectories(string? path, string extension)
        {
            var result = new List<string>();

            var files = Directory.GetFiles(path, $"*.{extension}");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var directoryName = Path.GetFileName(path);

                if (fileName != directoryName)
                    continue;

                result.Add(file);
            }

            string?[] directories = Directory.GetDirectories(path);
            foreach (var directory in directories)
                result = result.Concat(FindFilesInNestedDirectories(directory, extension)).ToList();

            return result;
        }

        private static List<string> FindAllZips(string? path)
        {
            var result = new List<string>();

            var files = Directory.GetFiles(path, "*.zip");
            foreach (var file in files)
                result.Add(file);

            string?[] directories = Directory.GetDirectories(path);
            foreach (var directory in directories)
                result = result.Concat(FindAllZips(directory)).ToList();

            return result;
        }

        private static void ExtractZip(string? path)
        {
            var zipPath = path;
            var extractPath = Path.GetDirectoryName(path);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath);
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("already exists"))
                {
                    Console.WriteLine(ex.ToString());
                    return;
                }
                Console.WriteLine($"File: {path} already exists\tSKIPPING...");
            }
        }

        private static void RemoveNonCsvFiles(string? path)
        {
            if (path == null)
                return;

            var allFiles = Directory.GetFiles(path);

            foreach (var file in allFiles)
            {
                var extension = Path.GetExtension(file);
                if (extension == null || extension == ".csv")
                    continue;

                Console.WriteLine($"Usunięto plik: {file}");
                File.Delete(file);
            }

            string?[] directories = Directory.GetDirectories(path);
            foreach (var directory in directories)
                RemoveNonCsvFiles(directory);
        }

        public static void ProcessFile(string path)
        {
            const string searchChar = "@";
            const string inputsKeyword = "@inputs";
            const string outputsKeyword = "@outputs";

            var lines = File.ReadAllLines(path);

            var result = lines.Where(x => !x.Contains(searchChar)).ToList();

            var inputs = lines
                .SingleOrDefault(x => x.Contains(inputsKeyword))
                ?.Replace(inputsKeyword, string.Empty)
                .Trim();

            var outputs = lines
                .SingleOrDefault(x => x.Contains(outputsKeyword))
                ?.Replace(outputsKeyword, string.Empty)
                .Trim();

            if (outputs != null && inputs != null)
            {
                var columns = inputs + ", " + outputs;
                result.Insert(0, columns);
            }

            var fileName = Path.GetFileNameWithoutExtension(path);
            path = Path.Combine(Path.GetDirectoryName(path) ?? "./", $"{fileName}.csv");

            File.WriteAllLines(path, result);
        }

        private static bool IsUserAnswerValid(string? userInput) => userInput is (not null) or "y" or "n";

        private static void MoveAllFilesToOutputDirectory(List<string> files, string? outputPath)
        {
            if (outputPath == null)
                return;

            Parallel.ForEach(files, file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var newFileName = $"{fileName}.csv";

                var path = Path.GetDirectoryName(file);
                var csvFile = Path.Combine(path, newFileName);
                var outputCsv = Path.Combine(outputPath, newFileName);

                File.Move(csvFile, outputCsv);
            });
        }

        private static Options OptionsInput(IReadOnlyList<string?> args)
        {
            string? directory = args.Count > 0 ? args[0] : null;

            if (directory == null)
            {
                Console.WriteLine("Please enter the directory:");
                var dialog = new FolderBrowserDialog();
                if (DialogResult.OK == dialog.ShowDialog())
                {
                    directory = dialog.SelectedPath;
                    Console.WriteLine(directory);
                }
            }

            Console.WriteLine("\nDo you want to delete .dat files after processing them? (y/n)");

            string? deleteDatFiles = Console.ReadLine();
            while (!IsUserAnswerValid(deleteDatFiles))
            {
                Console.WriteLine("Please enter a valid input (y/n)");
                deleteDatFiles = Console.ReadLine();
            }

            Console.WriteLine("\nDo you want to move the processed files to a new directory? (y/n)");

            string? moveFiles = Console.ReadLine();
            while (!IsUserAnswerValid(moveFiles))
            {
                Console.WriteLine("Please enter a valid input (y/n)");
                moveFiles = Console.ReadLine();
            }

            //If user wants to move files then prompt them for directory
            string? outputDirectory = null;
            if (moveFiles == "y")
            {
                Console.WriteLine("\nPlease enter the directory:");
                var dialog = new FolderBrowserDialog();
                if (DialogResult.OK == dialog.ShowDialog())
                {
                    outputDirectory = dialog.SelectedPath;
                    Console.WriteLine(outputDirectory);
                }
            }

            Console.WriteLine("\nSummary of options:");
            Console.WriteLine($"Directory: {directory}");
            Console.WriteLine($"Delete .dat files: {deleteDatFiles}");
            Console.WriteLine($"Move files to new directory: {moveFiles}");
            Console.WriteLine($"Output directory: {outputDirectory}");
            Console.WriteLine("Do you want to continue? (y/n)");

            string? continueInput = Console.ReadLine();
            while (!IsUserAnswerValid(continueInput))
            {
                Console.WriteLine("Please enter a valid input (y/n)");
                continueInput = Console.ReadLine();
            }

            //if user wants to continue then return a struct with all options, else call OptionsInput again
            if (continueInput == "y")
            {
                return new Options
                {
                    Directory = directory,
                    DeleteNonCsvFiles = deleteDatFiles == "y",
                    MoveFiles = moveFiles == "y",
                    OutputDirectory = outputDirectory
                };
            }
            else
            {
                //This is not the best but i don't care
                return OptionsInput(args);
            }

            return new Options();
        }

        [STAThread]
        private static void Main(string?[] args)
        {
            var options = OptionsInput(args);

            //DEV:
            /*var options = new Options
            {
                MoveFiles = true,
                DeleteNonCsvFiles = false,
                Directory = @"C:\Users\mikiz\OneDrive\ATH-sem4\Metody Sztucznej Inteligencji\RapidMiner\Data\All-regression\All-regression",
                OutputDirectory = @"C:\Users\mikiz\OneDrive\ATH-sem4\Metody Sztucznej Inteligencji\RapidMiner\Data\dataOut"
            };
            */

            var zips = FindAllZips(options.Directory);
            Parallel.ForEach(zips, ExtractZip);

            var files = FindFilesInNestedDirectories(options.Directory, Options.Extension);
            Console.WriteLine($"Files found: {files.Count}\n");
            Parallel.ForEach(files, ProcessFile);

            if (options.DeleteNonCsvFiles)
            {
                Console.WriteLine("Deleting non .csv files!\n");
                RemoveNonCsvFiles(options.Directory);
            }

            if (options.MoveFiles)
                MoveAllFilesToOutputDirectory(files, options.OutputDirectory);

            DeleteEmptyDirectories(options.Directory);

            Console.WriteLine("Done!\n");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}