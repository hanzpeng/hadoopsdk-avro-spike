// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License.  You may obtain a copy
// of the License at http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED
// WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
// 
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.
namespace AvroBlobAppender
{
    using Microsoft.Hadoop.Avro;
    using Microsoft.Hadoop.Avro.Container;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage;
    using System.Text;
    using System.Globalization;

    /// <summary>
    /// Represents a stream Avro writer.
    /// </summary>
    /// <typeparam name="T">The type of Avro objects to be written into the output stream.</typeparam>
    public sealed class AvroBlobAppenderWriter<T> : IAvroWriter<T>
    {
        private readonly IAvroSerializer<T> serializer;
        private readonly Codec codec;
        private ObjectContainerHeader header;
        private readonly object locker;
        private volatile bool isHeaderWritten;
        private readonly Microsoft.WindowsAzure.Storage.Blob.CloudBlobClient client;
        private readonly Microsoft.WindowsAzure.Storage.Blob.CloudBlobContainer container;
        private readonly Microsoft.WindowsAzure.Storage.Blob.CloudBlockBlob blockBlob;
        private List<string> blockNames;
        private static readonly int MaxBlockSize = 4194304;
        //private static readonly string BlockIdFormat = "{0:000000}";
        //private static readonly string AvroHeaderBlockIdsMetadataKey = "AvroHeaderBlockIds";
        public byte[] syncMarker;


        public AvroBlobAppenderWriter(CloudBlockBlob blockBlob, bool leaveOpen, IAvroSerializer<T> serializer, Codec codec)
        {
            if (blockBlob == null)
            {
                throw new ArgumentNullException("blockBlob");
            }

            if (serializer == null)
            {
                throw new ArgumentNullException("serializer");
            }

            if (codec == null)
            {
                throw new ArgumentNullException("codec");
            }

            this.codec = codec;
            this.serializer = serializer;
            this.isHeaderWritten = false;
            this.locker = new object();
            this.blockBlob = blockBlob;
            this.SetupObjectContainerHeader();
        }

        private void SetupObjectContainerHeader()
        {
            // This method needs to set up the writer to do it's thing.
            // There are two use cases for this writer.
            // 1.  This is a brand new, "in-memory" representation of the writer.
            // 2.  This is an existing blob, so we need to get the object container from blob storage.

            // First, we need to check if the blob exists.
            if (!this.blockBlob.Exists())
            {
                // The blob does not exist, we just create a new header and we are good
                this.syncMarker = new byte[16];
                new Random().NextBytes(syncMarker);
                this.header = new ObjectContainerHeader(syncMarker)
                {
                    CodecName = this.codec.Name,
                    Schema = this.serializer.WriterSchema.ToString()
                };

                // Since we are treating everything as in memory, we're going to fake the block list since we know there are none as of now.
                this.blockNames = new List<string>();
            }
            else
            {
                // If the blob exists, we need to fetch our stuff so we can read
                this.ReadObjectContainerHeaderFromBlob();
            }
        }

        private void ReadObjectContainerHeaderFromBlob()
        {
            this.blockBlob.FetchAttributes();


            // Deserialize the header from blob storage.  We will only download the size of the header from blob storage.
            using (var stream = this.blockBlob.OpenRead())
            {
                using (var decoder = new BinaryDecoder(stream, true))
                {
                    this.header = ObjectContainerHeader.Read(decoder);
                }
            }

            // We also need to set up the block list for appending.  Since we already have it, let's use it.
            // Get the existing block list.
            var blockList = this.blockBlob.DownloadBlockList(BlockListingFilter.Committed);
            this.blockNames = blockList
                    .Select(lbi => lbi.Name)
                    .ToList();

            // Set this so the writer doesn't try to write the header again.
            this.isHeaderWritten = true;
        }

        /// <summary>
        ///     Closes this instance.
        /// </summary>
        public void Close()
        {
            //this.encoder.Dispose();
        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            this.Close();
        }

        public void SetMetadata(IDictionary<string, byte[]> metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }

            foreach (var record in metadata)
            {
                this.header.AddMetadata(record.Key, record.Value);
            }
        }

        [SuppressMessage(
            "Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Disposable is returned as the result of this operation.")]
        public Task<IAvroWriterBlock<T>> CreateBlockAsync()
        {
            var taskSource = new TaskCompletionSource<IAvroWriterBlock<T>>();
            AvroBufferWriterBlock<T> block = null;
            try
            {
                block = new AvroBufferWriterBlock<T>(this.serializer, this.codec);
                taskSource.SetResult(block);
                return taskSource.Task;
            }
            catch
            {
                if (block != null)
                {
                    block.Dispose();
                }
                throw;
            }
        }

        public Task<long> WriteBlockAsync(IAvroWriterBlock<T> block)
        {
            if (block == null)
            {
                throw new ArgumentNullException("block");
            }

            var taskSource = new TaskCompletionSource<long>();
            block.Flush();

            long result = 0;
            lock (this.locker)
            {
                if (!this.isHeaderWritten)
                {
                    // This is never used.
                    result = this.WriteHeader();
                }

                if (block.ObjectCount != 0)
                {
                    // Replace this with a MemoryStream, as we will be limited to the size of an Azure Blob Block
                    using (var stream = new MemoryStream())
                    {
                        using (var encoder = new BinaryEncoder(stream, true))
                        {
                            encoder.Encode(block.ObjectCount);
                            encoder.Encode(block.Content);
                            encoder.EncodeFixed(this.header.SyncMarker);
                            stream.Seek(0, SeekOrigin.Begin);
                            this.WriteStreamToBlobAsync(stream);
                        }
                    }
                }
            }

            taskSource.SetResult(result);
            return taskSource.Task;
        }

        private long WriteHeader()
        {
            long result = 0;
            if (this.isHeaderWritten)
            {
                // Just in case
                return result;
            }

            using (var stream = new MemoryStream())
            {
                using (var encoder = new BinaryEncoder(stream, true))
                {
                    this.header.Write(encoder);
                    // This is just to match the original code, even though result isn't used.
                    result = stream.Position;

                    // We need to set the metadata for the blob so we can start over.
                    // First, we figure out how many bytes were written for the header.
                    // Next, we integer divide that by the maximum block size, and add 1 for the remainder.
                    // Finally, we set the metadata for the blob to use a comma-delimited list of block ids
                    // When we have to restart, we get the metadata, download the block list, add up the sizes of the blocks
                    //   specified in the metadata, download that many bytes, deserialize the container header and continue.
                    stream.Seek(0, SeekOrigin.Begin);

                    List<string> blockIdsWritten = this.WriteStreamToBlobAsync(stream);

                    string headerBlocksString = string.Join(",", blockIdsWritten.ToArray());
                    // We may want to check the size here.  Metadata is limited to 8K, per
                    // http://msdn.microsoft.com/en-us/library/dd179404.aspx
                    this.blockBlob.Metadata["AvroHeaderBlockIds"] = headerBlocksString;
                    this.blockBlob.SetMetadata();
                }
            }

            this.isHeaderWritten = true;
            return result;
        }

        private List<string> WriteStreamToBlobAsync(Stream stream)
        {
            // We need to make sure we don't go over the size of a block
            List<string> blockIdsWritten = new List<string>();
            int numberOfBlocks = (int)(stream.Length / AvroBlobAppenderWriter<T>.MaxBlockSize) + 1;
            // We'll just use a 4K buffer, as it neatly divides the block size. :)
            byte[] buffer = new byte[4096];
            int bytesRead = 0;
            for (int i = 0; i < numberOfBlocks; i++)
            {
                using (var blockStream = new MemoryStream())
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    while ((bytesRead != 0) && ((blockStream.Length + buffer.Length) < AvroBlobAppenderWriter<T>.MaxBlockSize))
                    {
                        blockStream.Write(buffer, 0, bytesRead);
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }

                    if (bytesRead != 0)
                    {
                        // We have a dangling block
                        blockStream.Write(buffer, 0, bytesRead);
                    }

                    blockStream.Seek(0, SeekOrigin.Begin);
                    var blockIdLengh = blockNames.First().Length;
                    var blockIdNum = blockNames.Count + 1;
                    var blockId = blockIdNum.ToString().PadLeft(blockIdLengh, '0');
                    while (this.blockNames.Contains(blockId))
                    {
                        blockId = blockIdNum++.ToString().PadLeft(blockIdLengh, '0');
                    }
                    //var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));

                    //var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format(AvroBlobAppenderWriter<T>.BlockIdFormat, this.blockNames.Count + 1)));

                    this.blockNames.Add(blockId);
                    this.blockBlob.PutBlock(blockId, blockStream, null);
                    blockIdsWritten.Add(blockId);
                }
            }

            this.blockBlob.PutBlockList(this.blockNames);
            return blockIdsWritten;
        }

        public IAvroWriterBlock<T> CreateBlock()
        {
            return this.CreateBlockAsync().Result;
        }

        public long WriteBlock(IAvroWriterBlock<T> block)
        {
            return this.WriteBlockAsync(block).Result;
        }
    }
}
