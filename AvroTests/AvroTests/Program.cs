namespace AvroTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Hadoop.Avro;
    using Microsoft.Hadoop.Avro.Container;
    using Microsoft.ServiceBus.Messaging;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class Program
    {
        private const string StorageConnectionString =
            "DefaultEndpointsProtocol=https;AccountName=hanzstorage;AccountKey=w9TEpvGTusvFlGAdCoWdDrwqLzy6er0Zm5YKdDD0YTkQdOj3WufeVrgd2c8q8amLR0o6xD0tBChcIIA+DCgxXA==";

        private const string BlobContainerId = "hanzhdi";
        private const string blobName = "aaa/avrotest/test008";

        private static void Main1(string[] args)
        {
            //if (args.Length < 3)
            //{
            //    Console.WriteLine("Usage:");
            //    Console.WriteLine("AppendBlob <storage-connection-string> <path-to-blob> <text-to-append>");
            //    Console.WriteLine(@"(e.g. AppendBlob DefaultEndpointsProtocol=http;AccountName=myaccount;AccountKey=v1vf48...test/test.txt ""Hello, World!"")");
            //    Console.WriteLine(@"NOTE: AppendBlob doesn't work with development storage. Use a real cloud storage account.");
            //    Environment.Exit(1);
            //}

            try
            {
                var storageAccount = CloudStorageAccount.Parse(StorageConnectionString);
                var client = storageAccount.CreateCloudBlobClient();
                var container = client.GetContainerReference(BlobContainerId);
                var result = container.CreateIfNotExists();
                var blob = container.GetBlockBlobReference("aaa.txt");
                Console.WriteLine("Former contents:");
                Console.WriteLine(blob.DownloadText());

                List<string> blockIds = new List<string>();
                blockIds.AddRange(blob.DownloadBlockList(BlockListingFilter.Committed).Select(b => b.Name));
                Console.WriteLine("existing block ids");
                blockIds.ForEach(x => Console.WriteLine(x));
                var newId = Convert.ToBase64String(Encoding.Default.GetBytes(blockIds.Count.ToString()));
                blob.PutBlock(newId, new MemoryStream(Encoding.Default.GetBytes("Hello World!")), null);
                blockIds.Add(newId);
                blob.PutBlockList(blockIds);

                Console.WriteLine("New contents:");
                Console.WriteLine(blob.DownloadText());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            Console.Read();
        }

        private static void Main(string[] args)
        {
            ConsoleHost.WithOptions(
                new Dictionary<string, Func<CancellationToken, Task>>
                    {
                        { "generate schema", GenerateSchema },
                        { "create messages", CreateMessages },
                        { "append messages", ApendMessages },

                    });
        }

        private static async Task GenerateSchema(CancellationToken token)
        {
            var serializer = AvroSerializer.Create<MyDataType>();

            var schema = serializer.WriterSchema.ToString();
            //var printPretty = schema;
            var printPretty = JObject
                .Parse(schema)
                .ToString(Formatting.Indented);

            using (var writer = File.CreateText("avro.schema.json"))
            {
                await writer.WriteAsync(printPretty);
            }
        }


        private static async Task CreateMessages(CancellationToken token)
        {
            int numberOfAzureBlobBlocks = 1;
            var storageAccount = CloudStorageAccount.Parse(StorageConnectionString);
            var client = storageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference(BlobContainerId);
            var result = await container.CreateIfNotExistsAsync(token);
            var blob = container.GetBlockBlobReference(blobName);
            var blockIdList = new List<string>();
            for (int i = 0; i < numberOfAzureBlobBlocks; i++)
            {
                //var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));
                //var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0:00000000}", 1)));
                var blockId = 1.ToString().PadLeft(8, '0');
                blockIdList.Add(blockId);
                await PutBlockAsync(blockId, blob, i * 10, i * 10 + 10, token);
            }
            await blob.PutBlockListAsync(blockIdList, token);
        }
        private static async Task ApendMessages(CancellationToken token)
        {
            var storageAccount = CloudStorageAccount.Parse(StorageConnectionString);
            var client = storageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference(BlobContainerId);
            var result = await container.CreateIfNotExistsAsync(token);
            var blob = container.GetBlockBlobReference(blobName);


            List<string> blockIds = new List<string>();
            var blockList = await blob.DownloadBlockListAsync(
                        BlockListingFilter.Committed,
                        AccessCondition.GenerateEmptyCondition(),
                        new BlobRequestOptions(),
                        new OperationContext(), token);

            blockIds.AddRange(blockList.Select(b => b.Name));
            Console.WriteLine("existing block ids");
            blockIds.ForEach(x => Console.WriteLine(x));

            //var newId = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));
            //var newId = Convert.ToBase64String(Encoding.Default.GetBytes(blockIds.Count.ToString()));
            //var newId = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0:000000}", blockIds.Count + 1)));

            var blockIdLengh = blockIds.First().Length;
            var blockIdNum = blockIds.Count + 1;

            var blockId = blockIdNum.ToString().PadLeft(blockIdLengh, '0');
            while (blockIds.Contains(blockId))
            {
                blockId = blockIdNum++.ToString().PadLeft(blockIdLengh, '0');
            }

            blockIds.Add(blockId);
            Console.WriteLine("New block ids");
            blockIds.ForEach(x => Console.WriteLine(x));

            //await PutBlockAsync(newId, blob, 100, 110, token);
            //await blob.PutBlockListAsync(blockIds, token);
        }
        private static async Task PutBlockAsync(String blockId, CloudBlockBlob blob, int start, int end, CancellationToken token)
        {
            var initialMessages = Enumerable
                .Range(start, end)
                .Select(x => new MyDataType(x));

            using (var buffer = new MemoryStream())
            {
                using (var w = AvroContainer.CreateWriter<MyDataType>(buffer, Codec.Null))
                {
                    using (var writer = new SequentialWriter<MyDataType>(w, 24))
                    {
                        initialMessages
                            .ToList()
                            .ForEach(writer.Write);
                    }
                }

                await buffer.FlushAsync(token);

                //var blockId = "AAAAA";
                buffer.Seek(0, SeekOrigin.Begin);

                await blob.PutBlockAsync(blockId, buffer, string.Empty, token);
                //await blob.PutBlockAsync(blockId, new MemoryStream(Encoding.Default.GetBytes("Hello! " + start.ToString() + " - " + end.ToString() + "\r\n")), string.Empty, token);

                //await blob.PutBlockAsync(blockId, buffer, null);
                //await blob.PutBlockAsync(blockId, new MemoryStream(Encoding.Default.GetBytes("Hello! " + start.ToString() + " - " + end.ToString() + "\r\n")), null);

            }
        }
    }
}