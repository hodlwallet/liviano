using System;
using System.IO;
using Liviano.Exceptions;
using Liviano.Interfaces;
using Liviano.Managers;
using Liviano.Models;
using Liviano.Utilities;
using Newtonsoft.Json;

namespace Liviano
{
    public class FileSystemStorageProvider : IStorageProvider
    {
        private readonly string _Id;
        private const string _Extension = "json";

        private readonly string _Directory = "data";

        public string FilePath { get; private set; }

        public FileSystemStorageProvider(string id = null, string directory = "data")
        {
            Guard.NotEmpty(directory, nameof(directory));

            if (id != null)
            {
                _Id = id;
            }
            else
            {
                _Id = Guid.NewGuid().ToString();
            }

            _Directory = directory;

            FilePath = GetFilePath();

            Directory.CreateDirectory(_Directory);
        }

        public Wallet LoadWallet()
        {
            if (File.Exists(FilePath))
            {
                return JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(FilePath));
            }
            else
            {
                throw new WalletException($"Wallet file with name '{FilePath}' could not be found.");
            }
        }

        public void SaveWallet(Wallet wallet)
        {
            SaveWallet(wallet, true);
        }

        public void SaveWallet(Wallet wallet, bool saveBackupFile = true)
        {
            string guid = Guid.NewGuid().ToString();
            string newFilePath = $"{FilePath}.{guid}.new";
            string tempFilePath = $"{FilePath}.{guid}.temp";

            File.WriteAllText(newFilePath, JsonConvert.SerializeObject(wallet, Formatting.Indented));

            // If the file does not exist yet, create it.
            if (!File.Exists(FilePath))
            {
                File.Move(newFilePath, FilePath);

                if (saveBackupFile)
                {
                    File.Copy(FilePath, $"{FilePath}.bak", true);
                }

                return;
            }

            if (saveBackupFile)
            {
                File.Copy(FilePath, $"{FilePath}.bak", true);
            }

            // Delete the file and rename the temp file to that of the target file.
            File.Move(FilePath, tempFilePath);
            File.Move(newFilePath, FilePath);

            try
            {
                File.Delete(tempFilePath);
            }
            catch (IOException)
            {
                // Marking the file for deletion in the future.
                File.Move(tempFilePath, $"{FilePath}.{guid}.del");
            }
        }

        public bool WalletExists()
        {
            return File.Exists(GetFilePath());
        }

        private string GetFilePath()
        {
            return Path.Combine(_Directory, $"{_Id}.{_Extension}");
        }
    }
}
