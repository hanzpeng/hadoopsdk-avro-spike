using Microsoft.Hadoop.Avro.Container;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvroBlobAppender;
using Microsoft.Hadoop.Avro;

namespace AvroBlobAppender.Driver
{
    static public class AvroUtility
    {
        public static Tuple<byte[], byte[]> CreateAvroHeaderAndBlocks<TSchema>(List<TSchema> dataList, string schema, string codec)
        {
            byte[] avroHeaderAndBlocksByteArray = null;
            byte[] syncMarker = new byte[16];
            new Random().NextBytes(syncMarker);
            var objContainerHeader = new ObjectContainerHeader(syncMarker)
            {
                CodecName = codec,
                Schema = schema
            };

            using (var buffer = new MemoryStream())
            {
                // TODO refactor the above AvroBlobAppenderWriter class to expose the syncMarker
                AvroBlobAppender.AvroBlobAppenderWriter<TSchema> avroWriter = new AvroBlobAppender.AvroBlobAppenderWriter<TSchema>(null, false, AvroSerializer.Create<TSchema>(), Codec.Null);
                syncMarker = avroWriter.syncMarker;

                //using (var avroWriter = AvroContainer.CreateWriter<TSchema>(buffer, Codec.Null))
                {
                    using (var sequentialWriter = new SequentialWriter<TSchema>(avroWriter, 24))
                    {
                        dataList.ToList().ForEach(sequentialWriter.Write);
                    }
                }
                buffer.Seek(0, SeekOrigin.Begin);
                avroHeaderAndBlocksByteArray = buffer.ToArray();
            }

            return new Tuple<byte[], byte[]>(avroHeaderAndBlocksByteArray, syncMarker);
        }
    }
}
