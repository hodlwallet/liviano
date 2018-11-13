using System;
using System.Reflection;
using System.IO;
using Xunit;
using Xunit.Sdk;
using NBitcoin;

namespace Liviano.Tests.Liviano
{
    public class BeforeAfterFileStorageProviderTest : BeforeAfterTestAttribute
    {
        public override void Before(MethodInfo methodUnderTest)
        {
            try
            {
                Directory.Delete("data", recursive: true);
                Directory.Delete("fs_wallet_test", recursive: true);
            }
            catch
            {
            }
            
        }

        public override void After(MethodInfo methodUnderTest)
        {
            try
            {
                Directory.Delete("data", recursive: true);
                Directory.Delete("fs_wallet_test", recursive: true);
            }
            catch
            {
            }
        }
    }

    public class FileSystemStorageProviderTest
    {
        [Fact]
        [BeforeAfterFileStorageProviderTest]
        public void CreateWalletFileDirectoryTest()
        {
            FileSystemStorageProvider fs = new FileSystemStorageProvider(directory: "fs_wallet_test");

            var directoryFullPath = Path.GetFullPath("fs_wallet_test");

            Assert.True(Directory.Exists(directoryFullPath));
        }

        [Fact]
        [BeforeAfterFileStorageProviderTest]
        public void SaveWalletTest()
        {
            FileSystemStorageProvider fs = new FileSystemStorageProvider();
            Wallet w = new Wallet();

            fs.SaveWallet(w);

            // Must have 1 file the wallet filename
            Assert.True(Directory.Exists("data"));
            string[] fileNames = Directory.GetFiles("data"); // Must have 2 files, .json and .bak
            Assert.Equal(2, fileNames.Length);

            Assert.True(fs.WalletExists());

            foreach (string fileName in fileNames)
            {
                Assert.True(fileName.EndsWith(".json") || fileName.EndsWith(".bak"));
            }
        }

        [Fact]
        public void LoadWalletTest()
        {
            string id = Guid.NewGuid().ToString();
            
            FileSystemStorageProvider fs = new FileSystemStorageProvider(id: id);
            Wallet w = new Wallet();
            w.Name = "FS Stored Wallet";
            w.Network = Network.Main;

            fs.SaveWallet(w);

            FileSystemStorageProvider fs2 = new FileSystemStorageProvider(id: id);
            Wallet w2 = fs2.LoadWallet();

            Assert.Equal("FS Stored Wallet", w2.Name);
            Assert.Equal(Network.Main, w2.Network);
        }

        [Fact]
        public void WalletExistsTest()
        {
            string id = Guid.NewGuid().ToString();

            FileSystemStorageProvider fs = new FileSystemStorageProvider(id: id);
            Wallet w = new Wallet();
            w.Name = "FS Stored Wallet";
            w.Network = Network.Main;

            fs.SaveWallet(w);

            Assert.True(fs.WalletExists());

            File.Delete(Path.Combine("data", $"{id}.json"));

            Assert.False(fs.WalletExists());
        }
    }
}
