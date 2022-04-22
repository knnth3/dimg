
namespace dimg
{
    internal class ModifiedState
    {
        internal string DockerfileName { get; set; }
        internal List<string> AddedFiles { get; private set; } = new List<string>();
        internal bool IsModified
        {
            get
            {
                return !string.IsNullOrEmpty(DockerfileName);
            }
        }

        /// <summary>
        /// Removes all created files
        /// </summary>
        internal void Cleanup()
        {
            foreach (var file in AddedFiles)
            {
                if (File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Failed to delete {file}");
                    }
                }
            }

            AddedFiles.Clear();
        }
    }

    static internal class DFModifier
    {
        private static readonly string m_NewDockerfileName = "Dockerfile.dimg";

        static internal ModifiedState AddImport(string dockerfilePath, string importFilePath)
        {
            var changes = new ModifiedState();
            string oldDockerfilePath = $"{dockerfilePath}/Dockerfile";
            if (!File.Exists(oldDockerfilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unable to find Dockerfile in '{dockerfilePath}'");
                Console.ResetColor();
                return changes;
            }

            if (!File.Exists(importFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unable to find file '{importFilePath}'");
                Console.ResetColor();
                return changes;
            }

            string filename = Path.GetFileName(importFilePath);
            string newFile = $"{dockerfilePath}/embed.dimg";
            try
            {
                changes.AddedFiles.Add(newFile);
                File.Copy(importFilePath, newFile, true);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.Error.WriteLine("Failed to copy embed.");
                Console.ResetColor();

                changes.Cleanup();
                return changes;
            }

            string text = $"ADD ./embed.dimg ./{filename}";
            string newDockerfilePath = $"{dockerfilePath}/{m_NewDockerfileName}";
            try
            {
                using StreamReader sr = File.OpenText(oldDockerfilePath);
                using StreamWriter sw = new StreamWriter(File.Open(newDockerfilePath, FileMode.Create));
                changes.AddedFiles.Add(newDockerfilePath);
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    sw.WriteLine(line);
                    if (line.StartsWith("WORKDIR"))
                    {
                        sw.WriteLine(text);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.WriteLine("Failed to create temp embed dockerfile");
                Console.ResetColor();

                changes.Cleanup();
                return changes;
            }

            changes.DockerfileName = m_NewDockerfileName;
            return changes;
        }
    }
}
