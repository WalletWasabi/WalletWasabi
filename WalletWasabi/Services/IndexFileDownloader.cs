using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.TorSocks5;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Services
{
	public class IndexFileDownloader
	{
		public Network Network { get; }
        public Uri AssetsUri { get; }
        public string FileName { get; }

        public IndexFileDownloader(Network network, Uri assetsUri, string fileName)
        {
            Network = network;
            AssetsUri = assetsUri;
            FileName = fileName; 
        }


        public async Task DownloadAsync()
        {
            var filePath = Path.Combine(Network.Name, FileName);
            var localFilePath = Path.Combine("./", FileName);
            var remoteFilePath = Path.Combine("Assets", filePath); 

            if(File.Exists(localFilePath))
            {
                var localSize = await GetLocalFileSize(localFilePath) ?? -1;
                var remoteSize = await GetRemoteFileSize(remoteFilePath) ?? -1;
                if(localSize < remoteSize)
                {
                    await DownloadFileAsync(remoteFilePath, localFilePath);
                }
            }
            else
            {
                await DownloadFileAsync(remoteFilePath, localFilePath);                
            }
        }

        private async Task DownloadFileAsync(string remoteFilePath, string localFilePath)
        {
			using (var torClient = new TorHttpClient(AssetsUri))
            {
                var response = await torClient.SendAsync(HttpMethod.Get, remoteFilePath);

                if(response.IsSuccessStatusCode)
                {
                    using(var remoteStream = await response.Content.ReadAsStreamAsync())
                    {
                        using(var fileStream = File.OpenWrite(localFilePath))
                        {
                            await remoteStream.CopyToAsync(fileStream);
                        }
                    }
                    
                }
            }            
        }

        private async Task<long?> GetLocalFileSize(string filePath)
        {
            try
            {
                return new FileInfo(filePath).Length;
            }
            catch(IOException)
            {
            }
            return null;
        }

        private async Task<long?> GetRemoteFileSize(string filePath)
        {
			using (var torClient = new TorHttpClient(AssetsUri))
            {
                var response = await torClient.SendAsync(HttpMethod.Head, filePath);

                if(response.IsSuccessStatusCode)
                {
                    return response.Content.Headers.ContentLength;
                }
                return null;
            }
        }
    }
}