using CommandLine;
using CommandLine.Text;

namespace dimg
{
    public class Application
    {
        public class Options
        {
            [Option('v', "version", Required = false, HelpText = "The version to create the docker image with.")]
            public string Version { get; set; }

            [Option('f', "folderpath", Required = true, HelpText = "The path to the folder that contains the dockerfile used for image building.")]
            public string DockerFilePath { get; set; }

            [Option('n', "name", Required = true, HelpText = "The output name of the image.")]
            public string Name { get; set; }

            [Option('r', "registry", Required = false, HelpText = "The registry to upload the image to")]
            public string Registry { get; set; }

            [Option("upload", Required = false, HelpText = "Auto-upload image to the given registry if specified.")]
            public bool AutoUpload { get; set; }

            [Option("noupgrade", Required = false, HelpText = "Prevents the generated file from asking for a newer version.")]
            public bool NoUpgrade { get; set; }

            [Option("cleanup", Required = false, HelpText = "Removes the image after building/uploading.")]
            public bool Cleanup { get; set; } = false;

            [Option("export", Required = false, HelpText = "Export the image as a tar file.")]
            public bool Export { get; set; } = false;

            [Option("compress", Required = false, HelpText = "Compress the exported tar file.")]
            public bool Compress { get; set; } = false;

            [Option('e', "embed", Required = false, HelpText = "Embed a file in the 1st WORKDIR specified in the dockerfile.")]
            public string Embed { get; set; }

            [Usage(ApplicationAlias = "dimg")]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Build using a specified version", new Options
                        {
                            Name = "CONTAINER_NAME",
                            Version = "1.0.2",
                            DockerFilePath = "C:/.../CONTAINER_FOLDER_NAME",
                            Registry = "registry.digitalocean.com/MY_NAME"
                        })
                    };
                }
            }
        }

        public static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);
            result.WithParsed(TryRunDIMGProcess);
        }

        private static void TryRunDIMGProcess(Options options)
        {
            bool success = RunDIMG(options);
            Console.ResetColor();
            if (success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Error.WriteLine("DIMG has finished successfully.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("An Error occured while running DIMG.");
                Console.ResetColor();
            }
        }

        private static bool RunDIMG(Options options)
        {
            options.DockerFilePath = options.DockerFilePath.Replace("\\\\", "/").Replace("\\", "/");
            if (options.DockerFilePath[options.DockerFilePath.Length - 1] == '/')
            {
                // Code assumes no trailing slash exists
                options.DockerFilePath = options.DockerFilePath.Substring(0, options.DockerFilePath.Length - 1);
            }

            string imageName;
            if (string.IsNullOrEmpty(options.Registry))
            {
                imageName = options.Name;
            }
            else
            {
                options.Registry = options.Registry.Replace("\\\\", "/").Replace("\\", "/");
                if (options.Registry[options.Registry.Length - 1] != '/')
                {
                    imageName = $"{options.Registry}/{options.Name}";
                }
                else
                {
                    imageName = $"{options.Registry}{options.Name}";
                }
            }

            ModifiedState state;
            if (!string.IsNullOrEmpty(options.Embed))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Embedding file '{options.Embed}' at root...");
                Console.ResetColor();

                state = DFModifier.AddImport(options.DockerFilePath, options.Embed);
                if (!state.IsModified)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to embed file");
                    Console.ResetColor();
                }
            }
            else
            {
                state = new ModifiedState();
            }

            string version = VersionControl.GetNextVersion(imageName, options.Version, options.NoUpgrade);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Building image '{imageName}:{version}'...");
            Console.ResetColor();

            string command;
            if (state.IsModified)
            {
                command = $"docker build -t {imageName}:{version} -f {options.DockerFilePath}/{state.DockerfileName} {options.DockerFilePath}";
            }
            else
            {
                command = $"docker build -t {imageName}:{version} {options.DockerFilePath}";
            }

            bool success = ConsoleHandle.RunCommand(command);
            if (state.IsModified)
            {
                state.Cleanup();
            }

            if (!success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("Failed to build docker image!");
                Console.ResetColor();
                return false;
            }

            if (options.Export)
            {
                string tarPath = $"{Environment.CurrentDirectory}\\{imageName} (v{version}).tar";
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Error.WriteLine($"Exporting image to '{tarPath}'...");
                Console.ResetColor();

                success = ConsoleHandle.RunCommand($"docker save --output \"{tarPath}\" {imageName}:{version}");
                if (!success)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Failed to export image!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Error.WriteLine($"Image has been saved.");
                    Console.ResetColor();
                    if (options.Compress)
                    {
                        string gzPath = $"{tarPath}.gz";
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Error.WriteLine($"Compressing image to '{gzPath}'...");
                        Console.ResetColor();
                        success = ConsoleHandle.RunCommand($"gzip --force --keep --stdout --best \"{tarPath}\" > \"{gzPath}\"");
                        if (!success)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Error.WriteLine("Failed to compress image. Make sure to have gzip installed.");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Error.WriteLine($"Image has been compressed. DIMG will now safely remove the uncompressed version.");
                            Console.ResetColor();
                            File.Delete(tarPath);
                        }
                    }
                }
            }

            TryUploadToRegistry(options, imageName, version);

            if (options.Cleanup)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Deleting created image...");
                Console.ResetColor();
                success = ConsoleHandle.RunCommand($"docker rmi {imageName}:{version}");
                if (!success)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Failed remove the built image!");
                    Console.ResetColor();
                }
            }

            return true;
        }

        private static void TryUploadToRegistry(Options options, string imageName, string version)
        {
            bool success;
            if (options.AutoUpload)
            {
                if (string.IsNullOrEmpty(options.Registry))
                {
                    Console.ResetColor();
                    Console.WriteLine("Auto-upload was set to true, but no registry was specified. Skipping upload...");
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Starting auto-upload...");
                Console.ResetColor();
                if (options.Registry.StartsWith("registry.digitalocean.com"))
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("Detected upload to digitalocean!");
                    Console.ResetColor();

                    success = ConsoleHandle.RunCommand("doctl registry login");
                    if (!success)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Failed to authenticate via 'doctl registry login'.");
                        Console.ResetColor();
                        return;
                    }

                    success = ConsoleHandle.RunCommand($"docker push {imageName}:{version}");
                    if (!success)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Failed to upload docker image to digitalocean!");
                        Console.ResetColor();
                        return;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Error.WriteLine("Detected upload to dockerhub!");
                    Console.ResetColor();

                    success = ConsoleHandle.RunCommand("docker login");
                    if (!success)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Failed to authenticate via 'docker login'.");
                        Console.ResetColor();
                        return;
                    }

                    success = ConsoleHandle.RunCommand($"docker push {imageName}:{version}");
                    if (!success)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Failed to upload docker image to dockerhub!");
                        Console.ResetColor();
                        return;
                    }
                }

                Console.ResetColor();
            }
        }
    }
}