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

        private readonly string _FilePath;

        private const string _Extension = "json";

        private readonly string _Directory = "data";

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

            _FilePath = GetFilePath();

            Directory.CreateDirectory(_Directory);
        }

        public Wallet LoadWallet()
        {
            if (File.Exists(_FilePath))
            {
                return JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(_FilePath));
            }
            else
            {
                throw new WalletException($"Wallet file with name '{_FilePath}' could not be found.");
            }
        }

        public void SaveWallet(Wallet wallet)
        {
            SaveWallet(wallet, true);
        }

        public void SaveWallet(Wallet wallet, bool saveBackupFile = true)
        {
            string guid = Guid.NewGuid().ToString();
            string newFilePath = $"{_FilePath}.{guid}.new";
            string tempFilePath = $"{_FilePath}.{guid}.temp";

            File.WriteAllText(newFilePath, JsonConvert.SerializeObject(wallet, Formatting.Indented));

            // If the file does not exist yet, create it.
            if (!File.Exists(_FilePath))
            {
                File.Move(newFilePath, _FilePath);

                if (saveBackupFile)
                {
                    File.Copy(_FilePath, $"{_FilePath}.bak", true);
                }

                return;
            }

            if (saveBackupFile)
            {
                File.Copy(_FilePath, $"{_FilePath}.bak", true);
            }

            // Delete the file and rename the temp file to that of the target file.
            File.Move(_FilePath, tempFilePath);
            File.Move(newFilePath, _FilePath);

            try
            {
                File.Delete(tempFilePath);
            }
            catch (IOException)
            {
                // Marking the file for deletion in the future.
                File.Move(tempFilePath, $"{_FilePath}.{guid}.del");
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
