using System;
using System.IO;
using Liviano.Utilities;
using Newtonsoft.Json;

namespace Liviano
{
    public class FileSystemWalletStorageProvider : IWalletStorageProvider
    {
        private readonly string id;

        private readonly string filePath;

        private const string extension = "json";

        private readonly string directory = "data";

        public FileSystemWalletStorageProvider(string id = null, string directory = "data")
        {
            Guard.NotEmpty(directory, nameof(directory));

            if (id != null)
            {
                this.id = id;
            }
            else
            {
                this.id = Guid.NewGuid().ToString();
            }

            this.directory = directory;

            filePath = GetFilePath();

            Directory.CreateDirectory(this.directory);
        }

        public Wallet LoadWallet()
        {
            if (File.Exists(filePath))
            {
                return JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(filePath));
            }
            else
            {
                throw new WalletException($"Wallet file with name '{filePath}' could not be found.");
            }
        }

        public void SaveWallet(Wallet wallet)
        {
            SaveWallet(wallet, true);
        }

        public void SaveWallet(Wallet wallet, bool saveBackupFile = true)
        {
            string guid = Guid.NewGuid().ToString();
            string newFilePath = Path.Combine(directory, $"{filePath}.{guid}.new");
            string tempFilePath = Path.Combine(directory, $"{filePath}.{guid}.temp");

            File.WriteAllText(newFilePath, JsonConvert.SerializeObject(wallet, Formatting.Indented));

            // If the file does not exist yet, create it.
            if (!File.Exists(filePath))
            {
                File.Move(newFilePath, filePath);

                if (saveBackupFile)
                {
                    File.Copy(filePath, $"{filePath}.bak", true);
                }

                return;
            }

            if (saveBackupFile)
            {
                File.Copy(filePath, $"{filePath}.bak", true);
            }

            // Delete the file and rename the temp file to that of the target file.
            File.Move(filePath, tempFilePath);
            File.Move(newFilePath, filePath);

            try
            {
                File.Delete(tempFilePath);
            }
            catch (IOException)
            {
                // Marking the file for deletion in the future.
                File.Move(tempFilePath, $"{filePath}.{guid}.del");
            }
        }

        public bool WalletExists()
        {
            return File.Exists(GetFilePath());
        }

        private string GetFilePath()
        {
            return Path.Combine(directory, $"{id}.{extension}");
        }
    }
}