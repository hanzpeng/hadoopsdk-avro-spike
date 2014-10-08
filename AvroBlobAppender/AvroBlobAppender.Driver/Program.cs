using Microsoft.Hadoop.Avro;
using Microsoft.Hadoop.Avro.Container;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AvroBlobAppender.Driver
{
    class Program
    {
        static void Main(string[] args)
        {
            Program.SerializeDeserializeObjectUsingReflectionStream();
            //SerializeDeserializeObjectUsingReflection();
            Console.ReadLine();
        }

        public static void SerializeDeserializeObjectUsingReflectionStream()
        {
            string blobName = "aaa/avrotest/test008";
            CloudBlobClient client = new Microsoft.WindowsAzure.Storage.Blob.CloudBlobClient(
                new Uri("http://hanzstorage.blob.core.windows.net"),
                new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(
                    "hanzstorage",
                    "w9TEpvGTusvFlGAdCoWdDrwqLzy6er0Zm5YKdDD0YTkQdOj3WufeVrgd2c8q8amLR0o6xD0tBChcIIA+DCgxXA=="
                    ));
            CloudBlobContainer container = client.GetContainerReference("hanzhdi");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
            foreach (var md in blockBlob.Metadata)
            {
                Console.WriteLine("{0}    {1}", md.Key, md.Value);
            }

            Console.WriteLine("Serializing Sample Data Set USING REFLECTION\n");


            AvroBlobAppender.AvroBlobAppenderWriter<MyDataType> writer =
                new AvroBlobAppender.AvroBlobAppenderWriter<MyDataType>(blockBlob, false, AvroSerializer.Create<MyDataType>(), Codec.Null);

            Microsoft.Hadoop.Avro.Container.SequentialWriter<MyDataType> sequentialWriter =
                new SequentialWriter<MyDataType>(writer, 10000);

            List<MyDataType> myDataList = MyDataType.GenerateData(555, 10);
            foreach (var myData in myDataList)
            {
                sequentialWriter.Write(myData);
            }

            sequentialWriter.Flush();
            sequentialWriter.Dispose();

            #region commented code
            //blockBlob.DownloadToFile(blobName, FileMode.Create);
            //using (Stream stream = File.OpenRead(blobName))
            //{
            //    Microsoft.Hadoop.Avro.Container.SequentialReader<MyDataType> reader =
            //        new Microsoft.Hadoop.Avro.Container.SequentialReader<MyDataType>(AvroContainer.CreateReader<MyDataType>(stream));
            //    List<MyDataType> actuals = reader.Objects.ToList();
            //    Console.WriteLine("Number of objects: {0}", actuals.Count);
            //    for (int i = 0; i < actuals.Count; i++)
            //    {
            //        var actual = actuals[i];
            //        MyDataType exp = null;
            //        switch (i)
            //        {
            //            case 0:
            //                exp = expected;
            //                break;
            //            case 1:
            //                exp = expected2;
            //                break;
            //            default:
            //                Console.WriteLine("No expected for object {0}", i);
            //                continue;
            //        }

            //        Console.WriteLine("Result of Data Set Identity Comparison is {0}", Program.Equal(exp, actual));
            //    }
            //}
            #endregion
        }

        //public static void SerializeDeserializeObjectUsingReflection()
        //{

        //    Console.WriteLine("SERIALIZATION USING REFLECTION\n");
        //    Console.WriteLine("Serializing Sample Data Set...");

        //    //Create a new AvroSerializer instance and specify a custom serialization strategy AvroDataContractResolver 
        //    //for serializing only properties attributed with DataContract/DateMember 
        //    var avroSerializer = AvroSerializer.Create<MyDataType>();

        //    //Create a Memory Stream buffer 
        //    using (var buffer = new MemoryStream())
        //    {
        //        //Create a data set using sample Class and struct  
        //        var expected = new MyDataType { SomeNumber = 111, SomeString = "My test string" };

        //        //Serialize the data to the specified stream 
        //        avroSerializer.Serialize(buffer, expected);


        //        Console.WriteLine("Deserializing Sample Data Set...");

        //        //Prepare the stream for deserializing the data 
        //        buffer.Seek(0, SeekOrigin.Begin);

        //        //Deserialize data from the stream and cast it to the same type used for serialization 
        //        var actual = avroSerializer.Deserialize(buffer);

        //        Console.WriteLine("Comparing Initial and Deserialized Data Sets...");

        //        //Finally, verify that deserialized data matches the original one 
        //        bool isEqual = Program.Equal(expected, actual);

        //        Console.WriteLine("Result of Data Set Identity Comparison is {0}", isEqual);

        //    }
        //}
    }
}
